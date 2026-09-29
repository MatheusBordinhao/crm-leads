using System.Text.Json;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Cada vez que um lead chega (integração, cadastro manual ou importação), com os dados de rastreio dessa
/// entrada e o payload bruto. A mesma origem e o mesmo id externo só são processados uma vez.
/// </summary>
public class EntradaRecebida
{
    public const int TamanhoMaximoErro = 500;

    public long Id { get; set; }

    /// <summary>Nulo só quando a situação é erro.</summary>
    public long? LeadId { get; set; }

    public Origem Origem { get; set; }

    /// <summary>Id do lead na Meta ou no Google, ou o UUID gerado pelo n8n no site. Nulo no cadastro manual.</summary>
    public string? IdExterno { get; set; }

    public int? CampanhaId { get; set; }

    /// <summary>Conjunto de anúncios da Meta ou grupo de anúncios do Google: <c>{ id, nome }</c>.</summary>
    public JsonDocument? Conjunto { get; set; }

    public JsonDocument? Anuncio { get; set; }

    public JsonDocument? Formulario { get; set; }

    public string? UtmSource { get; set; }

    public string? UtmMedium { get; set; }

    public string? UtmCampaign { get; set; }

    public string? UtmContent { get; set; }

    public string? UtmTerm { get; set; }

    public string? Gclid { get; set; }

    public JsonDocument? RespostasExtras { get; set; }

    /// <summary>O corpo recebido, como veio. É dado pessoal e é apagado na anonimização.</summary>
    public JsonDocument? PayloadBruto { get; set; }

    public SituacaoEntrada Situacao { get; set; }

    public string? Erro { get; set; }

    public bool Teste { get; set; }

    /// <summary>Quando a origem recebeu o lead (informado pela integração).</summary>
    public DateTimeOffset RecebidoEm { get; set; }

    /// <summary>Quando o CRM processou a entrada.</summary>
    public DateTimeOffset ProcessadoEm { get; set; }
}

internal sealed class EntradaRecebidaConfiguracao : IEntityTypeConfiguration<EntradaRecebida>
{
    public void Configure(EntityTypeBuilder<EntradaRecebida> builder)
    {
        builder.ToTable("entradas_recebidas");
        builder.Property(e => e.Origem).ComoTexto();
        builder.Property(e => e.IdExterno).HasMaxLength(Campanha.TamanhoMaximoIdExterno);
        builder.Property(e => e.UtmSource).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.UtmMedium).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.UtmCampaign).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.UtmContent).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.UtmTerm).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.Gclid).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(e => e.Situacao).ComoTexto(20);
        builder.Property(e => e.Erro).HasMaxLength(EntradaRecebida.TamanhoMaximoErro);

        // Excluir um lead de teste leva junto as entradas dele.
        builder.HasOne<Lead>().WithMany().HasForeignKey(e => e.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Campanha>().WithMany().HasForeignKey(e => e.CampanhaId).OnDelete(DeleteBehavior.Restrict);

        // Idempotência: a mesma origem e o mesmo id externo só entram uma vez, a não ser que a primeira tenha dado erro.
        builder.HasIndex(e => new { e.Origem, e.IdExterno }).IsUnique().HasFilter("situacao <> 'erro'");
        builder.HasIndex(e => e.RecebidoEm);
    }
}
