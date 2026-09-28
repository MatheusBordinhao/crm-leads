using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Infra.Auditoria;

/// <summary>Uma linha da auditoria. <see cref="Detalhes"/> nunca leva dados pessoais.</summary>
public class RegistroAuditoria
{
    public long Id { get; set; }

    /// <summary>Quem fez a ação. Nulo no login com e-mail desconhecido.</summary>
    public Guid? UsuarioId { get; set; }

    public required string Acao { get; set; }

    public string? Entidade { get; set; }

    public string? EntidadeId { get; set; }

    public JsonDocument? Detalhes { get; set; }

    public string? Ip { get; set; }

    public DateTimeOffset OcorridoEm { get; set; }
}

internal sealed class RegistroAuditoriaConfiguracao : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("auditoria");
        builder.Property(r => r.Acao).HasMaxLength(50);
        builder.Property(r => r.Entidade).HasMaxLength(50);
        builder.Property(r => r.EntidadeId).HasMaxLength(50);
        builder.Property(r => r.Ip).HasMaxLength(45);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => r.OcorridoEm);
        builder.HasIndex(r => new { r.Entidade, r.EntidadeId });
    }
}
