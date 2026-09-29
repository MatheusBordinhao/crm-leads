using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Leads;

/// <summary>Campanha de anúncios, única por origem e id externo. Uma campanha desconhecida é criada na entrada.</summary>
public class Campanha
{
    public const int TamanhoMaximoIdExterno = 100;

    public const int TamanhoMaximoNome = 200;

    public int Id { get; set; }

    public Origem Origem { get; set; }

    public required string IdExterno { get; set; }

    public required string Nome { get; set; }

    /// <summary>
    /// O nome foi gerado porque a campanha chegou sem nome (o Google manda só o id). Um nome recebido depois
    /// substitui o provisório, e o admin pode renomear.
    /// </summary>
    public bool NomeProvisorio { get; set; }

    public static string GerarNomeProvisorio(Origem origem, string idExterno) =>
        $"{Origens.NomeLegivel(origem)} · campanha {idExterno}";
}

internal sealed class CampanhaConfiguracao : IEntityTypeConfiguration<Campanha>
{
    public void Configure(EntityTypeBuilder<Campanha> builder)
    {
        builder.ToTable("campanhas");
        builder.Property(c => c.Origem).ComoTexto();
        builder.Property(c => c.IdExterno).HasMaxLength(Campanha.TamanhoMaximoIdExterno);
        builder.Property(c => c.Nome).HasMaxLength(Campanha.TamanhoMaximoNome);
        builder.HasIndex(c => new { c.Origem, c.IdExterno }).IsUnique();
    }
}
