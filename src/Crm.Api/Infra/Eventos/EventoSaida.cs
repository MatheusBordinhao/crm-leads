using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Infra.Eventos;

/// <summary>Tipos de evento do CRM para o n8n (seção 4.3). Os valores começam em 1: o zero não é um tipo válido.</summary>
public enum TipoEvento
{
    [JsonStringEnumMemberName("lead.criado")]
    LeadCriado = 1,

    [JsonStringEnumMemberName("lead.entrada_repetida")]
    LeadEntradaRepetida = 2,

    [JsonStringEnumMemberName("lead.reatribuido")]
    LeadReatribuido = 3,

    [JsonStringEnumMemberName("lead.etapa_alterada")]
    LeadEtapaAlterada = 4,
}

public enum SituacaoEvento
{
    /// <summary>Esperando a entrega, a primeira ou uma nova tentativa.</summary>
    [JsonStringEnumMemberName("pendente")]
    Pendente = 1,

    [JsonStringEnumMemberName("entregue")]
    Entregue = 2,

    /// <summary>Todas as tentativas falharam; os admins foram avisados.</summary>
    [JsonStringEnumMemberName("falhou")]
    Falhou = 3,
}

/// <summary>
/// Um evento para o n8n (seção 4.3), gravado na mesma transação da mudança e entregue depois, em segundo plano.
/// O payload é o JSON que vai para o n8n, montado na hora do evento: leva o nome e a empresa do lead, mas nunca
/// e-mail, telefone ou CPF/CNPJ.
/// </summary>
public class EventoSaida
{
    public const int TamanhoMaximoErro = 500;

    public Guid Id { get; set; }

    public TipoEvento Tipo { get; set; }

    /// <summary>O lead do evento: liga o aviso de falha ao lead e permite achar os eventos de um titular (LGPD).</summary>
    public long? LeadId { get; set; }

    public required JsonDocument Payload { get; set; }

    public SituacaoEvento Situacao { get; set; }

    public int Tentativas { get; set; }

    public DateTimeOffset ProximaTentativaEm { get; set; }

    public DateTimeOffset? EntregueEm { get; set; }

    /// <summary>O código HTTP ou o tipo da falha, nunca o corpo da resposta, que poderia repetir dados pessoais.</summary>
    public string? UltimoErro { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}

internal sealed class EventoSaidaConfiguracao : IEntityTypeConfiguration<EventoSaida>
{
    public void Configure(EntityTypeBuilder<EventoSaida> builder)
    {
        builder.ToTable("eventos_saida");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Tipo).ComoTexto();
        builder.Property(e => e.Situacao).ComoTexto(20);
        builder.Property(e => e.UltimoErro).HasMaxLength(EventoSaida.TamanhoMaximoErro);

        // Excluir um lead de teste leva junto os eventos dele (lead de teste não gera evento, mas por garantia).
        builder.HasOne<Lead>().WithMany().HasForeignKey(e => e.LeadId).OnDelete(DeleteBehavior.Cascade);

        // A fila do despachante: os pendentes, pela hora da próxima tentativa.
        builder.HasIndex(e => new { e.Situacao, e.ProximaTentativaEm });
    }
}
