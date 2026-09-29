using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Funil;

/// <summary>Motivo escolhido ao levar um lead para Perdido. A lista é configurável pelo admin.</summary>
public class MotivoPerda
{
    public const int TamanhoMaximoNome = 100;

    public int Id { get; set; }

    public required string Nome { get; set; }

    public bool Ativo { get; set; } = true;
}

internal sealed class MotivoPerdaConfiguracao : IEntityTypeConfiguration<MotivoPerda>
{
    public void Configure(EntityTypeBuilder<MotivoPerda> builder)
    {
        builder.ToTable("motivos_perda");
        builder.Property(m => m.Nome).HasMaxLength(MotivoPerda.TamanhoMaximoNome);
    }
}
