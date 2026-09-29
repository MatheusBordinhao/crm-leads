using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Lead: uma pessoa ou empresa interessada. Origem, campanha, UTMs e respostas extras são as da primeira
/// entrada; cada entrada guarda as suas em <see cref="EntradaRecebida"/>.
/// </summary>
public class Lead
{
    public const int TamanhoMaximoNome = 150;

    public const int TamanhoMaximoEmpresa = 150;

    /// <summary>Telefone e documento guardam o valor como veio quando não são reconhecidos, então têm folga.</summary>
    public const int TamanhoMaximoTelefone = 50;

    public const int TamanhoMaximoDocumento = 50;

    public const int TamanhoMaximoPlataforma = 30;

    public const int TamanhoMaximoRastreio = 200;

    public const int TamanhoMaximoDetalhePerda = 500;

    public long Id { get; set; }

    public required string Nome { get; set; }

    public string? Email { get; set; }

    public string? Telefone { get; set; }

    public string? CpfCnpj { get; set; }

    public string? Uf { get; set; }

    public string? Empresa { get; set; }

    public int EtapaId { get; set; }

    public Etapa Etapa { get; set; } = null!;

    public Guid? ResponsavelId { get; set; }

    public Usuario? Responsavel { get; set; }

    public Origem Origem { get; set; }

    /// <summary><c>facebook</c>, <c>instagram</c>, <c>google</c> ou <c>site</c>.</summary>
    public string? Plataforma { get; set; }

    public int? CampanhaId { get; set; }

    public Campanha? Campanha { get; set; }

    public string? UtmSource { get; set; }

    public string? UtmMedium { get; set; }

    public string? UtmCampaign { get; set; }

    public string? UtmContent { get; set; }

    public string? UtmTerm { get; set; }

    public string? Gclid { get; set; }

    /// <summary>Perguntas extras do formulário e as respostas, como objeto JSON.</summary>
    public JsonDocument? RespostasExtras { get; set; }

    public int? MotivoPerdaId { get; set; }

    public MotivoPerda? MotivoPerda { get; set; }

    public string? MotivoPerdaDetalhe { get; set; }

    /// <summary>Lead de teste: sem responsável, visível só para admins e fora do painel e dos relatórios.</summary>
    public bool Teste { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    public DateTimeOffset AtualizadoEm { get; set; }

    public DateTimeOffset? PrimeiroContatoEm { get; set; }

    public DateTimeOffset? ConvertidoEm { get; set; }

    public DateTimeOffset? PerdidoEm { get; set; }

    public DateTimeOffset? AnonimizadoEm { get; set; }

    /// <summary>Concorrência otimista: o <c>xmin</c> do PostgreSQL, que muda a cada atualização da linha.</summary>
    public uint Versao { get; set; }
}

internal sealed class LeadConfiguracao : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads");
        builder.Property(l => l.Nome).HasMaxLength(Lead.TamanhoMaximoNome);
        builder.Property(l => l.Email).HasMaxLength(Emails.TamanhoMaximo);
        builder.Property(l => l.Telefone).HasMaxLength(Lead.TamanhoMaximoTelefone);
        builder.Property(l => l.CpfCnpj).HasMaxLength(Lead.TamanhoMaximoDocumento);
        builder.Property(l => l.Uf).HasMaxLength(2);
        builder.Property(l => l.Empresa).HasMaxLength(Lead.TamanhoMaximoEmpresa);
        builder.Property(l => l.Origem).ComoTexto();
        builder.Property(l => l.Plataforma).HasMaxLength(Lead.TamanhoMaximoPlataforma);
        builder.Property(l => l.UtmSource).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.UtmMedium).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.UtmCampaign).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.UtmContent).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.UtmTerm).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.Gclid).HasMaxLength(Lead.TamanhoMaximoRastreio);
        builder.Property(l => l.MotivoPerdaDetalhe).HasMaxLength(Lead.TamanhoMaximoDetalhePerda);
        builder.Property(l => l.Versao).IsRowVersion();

        builder.HasOne(l => l.Etapa).WithMany().HasForeignKey(l => l.EtapaId).OnDelete(DeleteBehavior.Restrict);
        // O nome é explícito porque a navegação é descoberta antes de a tabela do Identity ganhar o nome "usuarios".
        builder.HasOne(l => l.Responsavel).WithMany().HasForeignKey(l => l.ResponsavelId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_leads_usuarios_responsavel_id");
        builder.HasOne(l => l.Campanha).WithMany().HasForeignKey(l => l.CampanhaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(l => l.MotivoPerda).WithMany().HasForeignKey(l => l.MotivoPerdaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.Email);
        builder.HasIndex(l => l.Telefone);
        builder.HasIndex(l => l.CpfCnpj);
        builder.HasIndex(l => new { l.ResponsavelId, l.EtapaId });
        builder.HasIndex(l => l.CriadoEm);
    }
}
