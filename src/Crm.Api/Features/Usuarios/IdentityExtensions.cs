using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crm.Api.Features.Usuarios;

internal static class IdentityExtensions
{
    public static async Task<Perfil> ObterPerfilAsync(this UserManager<Usuario> usuarios, Usuario usuario) =>
        (await usuarios.GetRolesAsync(usuario)).Contains(nameof(Perfil.Admin)) ? Perfil.Admin : Perfil.Atendente;

    /// <summary>
    /// Passa os erros do Identity para o ModelState. <paramref name="campoDaSenha"/> recebe os erros de
    /// senha; e-mail duplicado ou inválido vai para <c>email</c>; o resto fica sem campo.
    /// </summary>
    public static void AdicionarErros(this ModelStateDictionary modelState, IdentityResult resultado, string campoDaSenha)
    {
        foreach (var descricao in resultado.Errors.Select(e => (Campo: Campo(e.Code, campoDaSenha), e.Description)).Distinct())
        {
            modelState.AddModelError(descricao.Campo, descricao.Description);
        }
    }

    private static string Campo(string codigo, string campoDaSenha) => codigo switch
    {
        _ when codigo.StartsWith("Password", StringComparison.Ordinal) => campoDaSenha,
        nameof(IdentityErrorDescriber.DuplicateEmail)
            or nameof(IdentityErrorDescriber.DuplicateUserName)
            or nameof(IdentityErrorDescriber.InvalidEmail)
            or nameof(IdentityErrorDescriber.InvalidUserName) => "email",
        _ => string.Empty,
    };
}
