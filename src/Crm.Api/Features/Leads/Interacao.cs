using System.Text.Json.Serialization;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Leads;

/// <summary>Tipo da interação (seção 3.5). Os valores começam em 1: o zero não é um tipo válido.</summary>
public enum TipoInteracao
{
    [JsonStringEnumMemberName("nota")]
    Nota = 1,

    [JsonStringEnumMemberName("ligacao")]
    Ligacao = 2,

    [JsonStringEnumMemberName("whatsapp")]
    Whatsapp = 3,

    [JsonStringEnumMemberName("email")]
    Email = 4,

    [JsonStringEnumMemberName("reuniao")]
    Reuniao = 5,
}

/// <summary>
/// Algo que a atendente fez com o lead: uma nota, uma ligação, uma mensagem ou uma reunião. Interações não são
/// editadas nem excluídas; uma correção vira uma nova nota. A exceção é a anonimização, que apaga a descrição.
/// </summary>
public class Interacao
{
    public const int TamanhoMaximoDescricao = 2000;

    public long Id { get; set; }

    public long LeadId { get; set; }

    /// <summary>Quem registrou.</summary>
    public Guid UsuarioId { get; set; }

    public TipoInteracao Tipo { get; set; }

    /// <summary>Dado pessoal: é apagada na anonimização. Obrigatória só na nota.</summary>
    public string? Descricao { get; set; }

    /// <summary>Quando aconteceu. Pode ser uma data passada, nunca futura.</summary>
    public DateTimeOffset OcorridoEm { get; set; }

    /// <summary>Quando foi registrada.</summary>
    public DateTimeOffset CriadoEm { get; set; }

    /// <summary>Ligação, WhatsApp, e-mail e reunião são contato com o lead; a nota não.</summary>
    public static bool EhContato(TipoInteracao tipo) => tipo != TipoInteracao.Nota;
}

internal sealed class InteracaoConfiguracao : IEntityTypeConfiguration<Interacao>
{
    public void Configure(EntityTypeBuilder<Interacao> builder)
    {
        builder.ToTable("interacoes");
        builder.Property(i => i.Tipo).ComoTexto(20);
        builder.Property(i => i.Descricao).HasMaxLength(Interacao.TamanhoMaximoDescricao);

        // Excluir um lead de teste leva junto as interações dele.
        builder.HasOne<Lead>().WithMany().HasForeignKey(i => i.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(i => i.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => new { i.LeadId, i.OcorridoEm });
    }
}
