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
    /// <summary>
    /// A etapa aberta ativa de menor ordem, onde os leads novos e os reabertos entram. A leitura trava a etapa
    /// (<c>FOR SHARE</c>) até o fim da transação, para ela não ser desativada enquanto um lead entra nela.
    /// </summary>
    public static async Task<Etapa> InicialAsync(this DbSet<Etapa> etapas, CancellationToken ct) =>
        (await etapas
            .FromSql($"SELECT * FROM etapas WHERE ativa AND tipo = 'aberta' ORDER BY ordem, id LIMIT 1 FOR SHARE")
            .ToListAsync(ct))
        .Single();

    /// <summary>
    /// A etapa ativa com o id, travada (<c>FOR SHARE</c>) até o fim da transação: quem a desativa espera, e um
    /// lead nunca vai para uma etapa desativada. Nulo se não existe ou está desativada.
    /// </summary>
    public static async Task<Etapa?> AtivaParaMoverAsync(this DbSet<Etapa> etapas, int id, CancellationToken ct) =>
        (await etapas.FromSql($"SELECT * FROM etapas WHERE id = {id} AND ativa FOR SHARE").ToListAsync(ct))
        .SingleOrDefault();
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
