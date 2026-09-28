using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Features.Usuarios;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(SignInManager<Usuario> signIn, UserManager<Usuario> usuarios) : ControllerBase
{
    public const string MensagemCredenciaisInvalidas = "E-mail ou senha incorretos.";

    public static readonly string MensagemBloqueado =
        $"Login bloqueado por {AcessoExtensions.MinutosDeBloqueio} minutos após " +
        $"{AcessoExtensions.TentativasAteBloqueio} tentativas erradas.";

    public const string MensagemDesativado = "Usuário desativado. Fale com um administrador.";

    /// <summary>Abre a sessão (cookie). Depois de 5 tentativas erradas, o login fica bloqueado por 15 minutos.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AcessoExtensions.PoliticaLimiteLogin)]
    public async Task<ActionResult<EuResponse>> Login(LoginRequest requisicao)
    {
        var usuario = await usuarios.FindByEmailAsync(requisicao.Email.Trim());
        if (usuario is null)
        {
            return FalhaNoLogin(MensagemCredenciaisInvalidas);
        }

        var resultado = await signIn.PasswordSignInAsync(usuario, requisicao.Senha, isPersistent: true, lockoutOnFailure: true);
        if (resultado.Succeeded)
        {
            return await EuAsync(usuario);
        }

        return FalhaNoLogin(resultado switch
        {
            { IsLockedOut: true } => MensagemBloqueado,
            { IsNotAllowed: true } => MensagemDesativado,
            _ => MensagemCredenciaisInvalidas,
        });
    }

    [HttpPost("logout")]
    [Authorize(Policy = Politicas.Sessao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }

    /// <summary>Usuário logado. Funciona mesmo com a senha provisória.</summary>
    [HttpGet("eu")]
    [Authorize(Policy = Politicas.Sessao)]
    public async Task<ActionResult<EuResponse>> Eu()
    {
        var usuario = await usuarios.GetUserAsync(User);
        return usuario is null ? Unauthorized() : await EuAsync(usuario);
    }

    /// <summary>Troca a senha do usuário logado e renova o cookie. Libera o sistema quando a senha era provisória.</summary>
    [HttpPost("trocar-senha")]
    [Authorize(Policy = Politicas.Sessao)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaRequest requisicao)
    {
        var usuario = await usuarios.GetUserAsync(User);
        if (usuario is null)
        {
            return Unauthorized();
        }

        // A senha atual errada conta para o bloqueio, como no login.
        var conferencia = await signIn.CheckPasswordSignInAsync(usuario, requisicao.SenhaAtual, lockoutOnFailure: true);
        if (!conferencia.Succeeded)
        {
            ModelState.AddModelError("senhaAtual", conferencia.IsLockedOut ? MensagemBloqueado : "Senha atual incorreta.");
            return ValidationProblem(ModelState);
        }

        usuario.TrocarSenha = false;
        var resultado = await usuarios.ChangePasswordAsync(usuario, requisicao.SenhaAtual, requisicao.NovaSenha);
        if (!resultado.Succeeded)
        {
            ModelState.AdicionarErros(resultado, campoDaSenha: "novaSenha");
            return ValidationProblem(ModelState);
        }

        await signIn.RefreshSignInAsync(usuario);
        return NoContent();
    }

    private async Task<EuResponse> EuAsync(Usuario usuario) =>
        new(usuario.Id, usuario.Nome, usuario.Email!, await usuarios.ObterPerfilAsync(usuario), usuario.TrocarSenha);

    private ObjectResult FalhaNoLogin(string mensagem) =>
        Problem(detail: mensagem, statusCode: StatusCodes.Status401Unauthorized, title: "Não foi possível entrar");
}
