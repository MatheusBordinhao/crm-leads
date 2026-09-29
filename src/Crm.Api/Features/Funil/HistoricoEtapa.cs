using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Funil;

/// <summary>
/// Uma mudança de etapa. Toda mudança grava uma linha, inclusive quando o sistema reabre um lead perdido
/// que voltou a entrar. O motivo de perda fica aqui mesmo depois de o lead sair de Perdido.
/// </summary>
public class HistoricoEtapa
{
    public long Id { get; set; }

    public long LeadId { get; set; }

    public int EtapaAnteriorId { get; set; }

    public int EtapaNovaId { get; set; }

    public int? MotivoPerdaId { get; set; }

    public string? MotivoPerdaDetalhe { get; set; }

    /// <summary>Quem mudou. Nulo quando foi o sistema, por exemplo numa entrada da integração.</summary>
    public Guid? UsuarioId { get; set; }

    public DateTimeOffset OcorridoEm { get; set; }
}

internal sealed class HistoricoEtapaConfiguracao : IEntityTypeConfiguration<HistoricoEtapa>
{
    public void Configure(EntityTypeBuilder<HistoricoEtapa> builder)
    {
        builder.ToTable("historico_etapas");
        builder.Property(h => h.MotivoPerdaDetalhe).HasMaxLength(Lead.TamanhoMaximoDetalhePerda);

        // Excluir um lead de teste leva junto o histórico dele.
        builder.HasOne<Lead>().WithMany().HasForeignKey(h => h.LeadId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Etapa>().WithMany().HasForeignKey(h => h.EtapaAnteriorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Etapa>().WithMany().HasForeignKey(h => h.EtapaNovaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MotivoPerda>().WithMany().HasForeignKey(h => h.MotivoPerdaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(h => h.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(h => h.OcorridoEm);
    }
}
