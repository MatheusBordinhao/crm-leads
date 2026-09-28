using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Infra.Web;

/// <summary>
/// Roda o validador do FluentValidation de cada argumento da action que tiver um e responde 400
/// (ValidationProblem), com os nomes dos campos em camelCase, como no JSON.
/// </summary>
internal sealed class ValidacaoFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
    {
        var servicos = contexto.HttpContext.RequestServices;
        foreach (var argumento in contexto.ActionArguments.Values)
        {
            if (argumento is null
                || servicos.GetService(typeof(IValidator<>).MakeGenericType(argumento.GetType())) is not IValidator validador)
            {
                continue;
            }

            var resultado = await validador.ValidateAsync(
                new ValidationContext<object>(argumento), contexto.HttpContext.RequestAborted);
            foreach (var erro in resultado.Errors)
            {
                contexto.ModelState.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(erro.PropertyName), erro.ErrorMessage);
            }
        }

        if (!contexto.ModelState.IsValid && contexto.Controller is ControllerBase controller)
        {
            contexto.Result = controller.ValidationProblem(contexto.ModelState);
            return;
        }

        await proximo();
    }
}
