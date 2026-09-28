using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Crm.Api.Features.Usuarios;

/// <summary>Usuário desativado não entra e, se já estiver logado, perde a sessão na próxima revalidação.</summary>
internal sealed class SignInManagerCrm(
    UserManager<Usuario> usuarios,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<Usuario> claimsFactory,
    IOptions<IdentityOptions> opcoes,
    ILogger<SignInManager<Usuario>> logger,
    IAuthenticationSchemeProvider esquemas,
    IUserConfirmation<Usuario> confirmacao)
    : SignInManager<Usuario>(usuarios, contextAccessor, claimsFactory, opcoes, logger, esquemas, confirmacao)
{
    public override async Task<bool> CanSignInAsync(Usuario user) => user.Ativo && await base.CanSignInAsync(user);

    public override async Task<Usuario?> ValidateSecurityStampAsync(ClaimsPrincipal? principal)
    {
        var usuario = await base.ValidateSecurityStampAsync(principal);
        return usuario is { Ativo: true } ? usuario : null;
    }
}
