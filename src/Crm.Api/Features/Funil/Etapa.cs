using System.Text.Json.Serialization;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Funil;

/// <summary>Tipo da etapa (seção 3.4). Os valores começam em 1: o zero não é um tipo válido.</summary>
public enum TipoEtapa
{
    [JsonStringEnumMemberName("aberta")]
    Aberta = 1,

    [JsonStringEnumMemberName("ganho")]
    Ganho = 2,

    [JsonStringEnumMemberName("perdido")]
    Perdido = 3,
}

/// <summary>
/// Etapa do funil. A etapa inicial é a aberta ativa de menor ordem, e existe sempre exatamente uma etapa
/// ativa do tipo ganho e uma do tipo perdido.
/// </summary>
public class Etapa
{
    public const int TamanhoMaximoNome = 50;

    public int Id { get; set; }

    public required string Nome { get; set; }

    public int Ordem { get; set; }

    public TipoEtapa Tipo { get; set; }

    public bool Ativa { get; set; } = true;
}

internal static class ConsultasEtapas
{
    /// <summary>A etapa aberta ativa de menor ordem, onde os leads novos e os reabertos entram.</summary>
    public static Task<Etapa> InicialAsync(this IQueryable<Etapa> etapas, CancellationToken ct) =>
        etapas
            .Where(e => e.Ativa && e.Tipo == TipoEtapa.Aberta)
            .OrderBy(e => e.Ordem)
            .ThenBy(e => e.Id)
            .FirstAsync(ct);
}

internal sealed class EtapaConfiguracao : IEntityTypeConfiguration<Etapa>
{
    public void Configure(EntityTypeBuilder<Etapa> builder)
    {
        builder.ToTable("etapas");
        builder.Property(e => e.Nome).HasMaxLength(Etapa.TamanhoMaximoNome);
        builder.Property(e => e.Tipo).ComoTexto();

        // No máximo uma etapa ativa de ganho e uma de perdido.
        builder.HasIndex(e => e.Tipo).IsUnique().HasFilter("ativa AND tipo <> 'aberta'");
    }
}
