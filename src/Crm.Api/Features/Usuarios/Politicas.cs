using Crm.Api.Features.Integracoes;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Features.Usuarios;

/// <summary>
/// Políticas de autorização. Todo controller declara a sua: <c>[Authorize]</c> (política padrão: logado e
/// com a senha definitiva), <c>[Authorize(Policy = Politicas.Admin)]</c>,
/// <c>[Authorize(Policy = Politicas.Sessao)]</c>, <c>[Authorize(Policy = Politicas.Integracao)]</c> ou
/// <c>[AllowAnonymous]</c>.
/// </summary>
public static class Politicas
{
    /// <summary>Admin logado e com a senha definitiva.</summary>
    public const string Admin = "Admin";

    /// <summary>Qualquer usuário logado, mesmo com a senha provisória. Só para as rotas de acesso.</summary>
    public const string Sessao = "Sessao";

    /// <summary>
    /// O n8n, pela chave de integração. Só para <c>/api/integracoes/*</c>: a política usa apenas o esquema da
    /// chave, então o cookie não vale nessas rotas, e as outras políticas não enxergam a chave.
    /// </summary>
    public const string Integracao = "Integracao";

    internal static void Configurar(AuthorizationBuilder autorizacao)
    {
        autorizacao
            .SetDefaultPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new SenhaDefinidaRequirement())
                .Build())
            .AddPolicy(Sessao, politica => politica.RequireAuthenticatedUser())
            .AddPolicy(Admin, politica => politica
                .RequireAuthenticatedUser()
                .AddRequirements(new SenhaDefinidaRequirement())
                .RequireRole(nameof(Perfil.Admin)))
            .AddPolicy(Integracao, politica => politica
                .AddAuthenticationSchemes(ChaveIntegracaoHandler.Esquema)
                .RequireAuthenticatedUser());
    }
}

/// <summary>Falha enquanto o usuário não trocar a senha provisória.</summary>
internal sealed class SenhaDefinidaRequirement : AuthorizationHandler<SenhaDefinidaRequirement>, IAuthorizationRequirement
{
    public const string Mensagem = "Troque a senha provisória para continuar.";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext contexto, SenhaDefinidaRequirement requisito)
    {
        if (!contexto.User.HasClaim(ClaimsUsuarioFactory.ClaimTrocarSenha, "true"))
        {
            contexto.Succeed(requisito);
        }

        return Task.CompletedTask;
    }
}
