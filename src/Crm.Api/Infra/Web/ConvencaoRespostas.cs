using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Crm.Api.Infra.Web;

/// <summary>
/// Respostas documentadas no OpenAPI, de onde saem os tipos do front: toda action tem um ProblemDetails como
/// resposta de erro e, se não declarar nenhum 2xx, um 200 com o tipo que devolve.
/// </summary>
internal sealed class ConvencaoRespostas : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var declara2xx = action.Filters.Concat(action.Controller.Filters)
            .OfType<IApiResponseMetadataProvider>()
            .Any(p => p.StatusCode is >= 200 and < 300);
        if (!declara2xx && TipoDevolvido(action.ActionMethod.ReturnType) is { } tipo)
        {
            action.Filters.Add(new ProducesResponseTypeAttribute(tipo, StatusCodes.Status200OK));
        }

        action.Filters.Add(new ProducesDefaultResponseTypeAttribute());
    }

    /// <summary>O <c>T</c> de <c>Task&lt;ActionResult&lt;T&gt;&gt;</c> e afins; nulo para IActionResult e void.</summary>
    private static Type? TipoDevolvido(Type tipo)
    {
        if (tipo.IsGenericType && (tipo.GetGenericTypeDefinition() == typeof(Task<>) || tipo.GetGenericTypeDefinition() == typeof(ValueTask<>)))
        {
            tipo = tipo.GetGenericArguments()[0];
        }
        else if (tipo == typeof(Task) || tipo == typeof(ValueTask) || tipo == typeof(void))
        {
            return null;
        }

        if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            return tipo.GetGenericArguments()[0];
        }

        return typeof(IActionResult).IsAssignableFrom(tipo) ? null : tipo;
    }
}
