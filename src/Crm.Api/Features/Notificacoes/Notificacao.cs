using System.Text.Json.Serialization;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Importacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Notificacoes;

/// <summary>O que a notificação avisa (seção 3.6). Os valores começam em 1: o zero não é um tipo válido.</summary>
public enum TipoNotificacao
{
    /// <summary>Um lead novo ficou com você, pelo rodízio ou pela escolha de um admin.</summary>
    [JsonStringEnumMemberName("lead_atribuido")]
    LeadAtribuido = 1,

    /// <summary>Um admin passou um lead para você.</summary>
    [JsonStringEnumMemberName("lead_reatribuido")]
    LeadReatribuido = 2,

    /// <summary>Um lead seu recebeu uma nova entrada. Quando o lead não tem responsável, vai para os admins.</summary>
    [JsonStringEnumMemberName("nova_entrada")]
    NovaEntrada = 3,

    /// <summary>Para os admins: uma atendente cadastrou à mão uma pessoa que já é lead de outra pessoa.</summary>
    [JsonStringEnumMemberName("cadastro_repetido")]
    CadastroRepetido = 4,

    /// <summary>Para os admins: um lead novo ficou sem responsável.</summary>
    [JsonStringEnumMemberName("lead_sem_responsavel")]
    LeadSemResponsavel = 5,

    /// <summary>Para os admins: um evento para o n8n falhou de vez.</summary>
    [JsonStringEnumMemberName("evento_falhou")]
    EventoFalhou = 6,

    /// <summary>Para o admin que importou um CSV: o resumo da importação, com o link para o resultado.</summary>
    [JsonStringEnumMemberName("importacao_concluida")]
    ImportacaoConcluida = 7,

    /// <summary>Quantos leads novos uma importação deixou com você.</summary>
    [JsonStringEnumMemberName("leads_importados")]
    LeadsImportados = 8,
}

/// <summary>
/// Notificação no sistema para um usuário (seção 3.6). O título não leva dados do lead: o nome vem da ficha na
/// leitura, e só para quem ainda vê o lead.
/// </summary>
public class Notificacao
{
    public const int TamanhoMaximoTitulo = 200;

    public long Id { get; set; }

    public Guid UsuarioId { get; set; }

    public TipoNotificacao Tipo { get; set; }

    public required string Titulo { get; set; }

    public long? LeadId { get; set; }

    /// <summary>A importação do resumo, que a notificação abre. Só no resumo para o admin que importou.</summary>
    public long? ImportacaoId { get; set; }

    public DateTimeOffset? LidaEm { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}

internal sealed class NotificacaoConfiguracao : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");
        builder.Property(n => n.Tipo).ComoTexto();
        builder.Property(n => n.Titulo).HasMaxLength(Notificacao.TamanhoMaximoTitulo);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(n => n.UsuarioId).OnDelete(DeleteBehavior.Restrict);

        // Excluir um lead de teste leva junto as notificações dele.
        builder.HasOne<Lead>().WithMany().HasForeignKey(n => n.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Importacao>().WithMany().HasForeignKey(n => n.ImportacaoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(n => new { n.UsuarioId, n.CriadoEm });
    }
}
