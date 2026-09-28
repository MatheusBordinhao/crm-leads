using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Crm.Api.Features.Usuarios;

/// <summary>
/// Quando o acesso é negado só porque a senha ainda é provisória, o 403 explica o motivo. Os demais casos
/// seguem o tratamento padrão (401 e 403 vazios, que viram ProblemDetails no UseStatusCodePages).
/// </summary>
internal sealed class AutorizacaoResultHandler(IProblemDetailsService problemas) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var senhaPendente = authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<SenhaDefinidaRequirement>().Any();
        if (authorizeResult.Forbidden && senhaPendente == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await problemas.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = { Status = StatusCodes.Status403Forbidden, Detail = SenhaDefinidaRequirement.Mensagem },
            });
            return;
        }

        await _padrao.HandleAsync(next, context, policy, authorizeResult);
    }
}
