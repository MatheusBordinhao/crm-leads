using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Api.Features.Usuarios;

/// <summary>Usuário do CRM. O e-mail, em minúsculas, também é o nome de usuário do Identity.</summary>
public class Usuario : IdentityUser<Guid>
{
    public const int TamanhoMaximoNome = 100;

    public required string Nome { get; set; }

    public bool Ativo { get; set; } = true;

    public bool ParticipaRodizio { get; set; }

    public DateTimeOffset? UltimoLeadAtribuidoEm { get; set; }

    /// <summary>A senha é provisória: o usuário precisa trocá-la antes de usar o sistema.</summary>
    public bool TrocarSenha { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
}

internal sealed class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder) =>
        builder.Property(u => u.Nome).HasMaxLength(Usuario.TamanhoMaximoNome);
}
