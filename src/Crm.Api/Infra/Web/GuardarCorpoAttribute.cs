using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Infra.Web;

/// <summary>
/// Guarda o corpo da requisição antes de o MVC lê-lo, para a action conseguir gravá-lo como veio
/// (<see cref="GuardarCorpoExtensions.LerCorpoGuardadoAsync"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class GuardarCorpoAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context) => context.HttpContext.Request.EnableBuffering();

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}

internal static class GuardarCorpoExtensions
{
    /// <summary>O corpo JSON recebido, como veio. Só funciona em actions com <see cref="GuardarCorpoAttribute"/>.</summary>
    public static async Task<JsonDocument> LerCorpoGuardadoAsync(this HttpRequest requisicao, CancellationToken ct)
    {
        requisicao.Body.Position = 0;
        return await JsonDocument.ParseAsync(requisicao.Body, cancellationToken: ct);
    }
}
