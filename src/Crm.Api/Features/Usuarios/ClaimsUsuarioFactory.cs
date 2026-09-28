using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Crm.Api.Features.Usuarios;

/// <summary>Acrescenta às claims do Identity a marca de senha provisória, conferida pela política padrão.</summary>
internal sealed class ClaimsUsuarioFactory(
    UserManager<Usuario> usuarios,
    RoleManager<IdentityRole<Guid>> perfis,
    IOptions<IdentityOptions> opcoes)
    : UserClaimsPrincipalFactory<Usuario, IdentityRole<Guid>>(usuarios, perfis, opcoes)
{
    public const string ClaimTrocarSenha = "crm:trocar_senha";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(Usuario usuario)
    {
        var identidade = await base.GenerateClaimsAsync(usuario);
        if (usuario.TrocarSenha)
        {
            identidade.AddClaim(new Claim(ClaimTrocarSenha, "true"));
        }

        return identidade;
    }
}
