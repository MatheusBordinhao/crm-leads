using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crm.Api.Infra.Web;

/// <summary>
/// Roda o validador do FluentValidation de cada argumento da action que tiver um e responde 400
/// (ValidationProblem), com os nomes dos campos em camelCase, como no JSON.
/// </summary>
/// <remarks>
/// Os erros vão num ModelState à parte. O da requisição já tem os parâmetros da query string com o nome da
/// propriedade (<c>Tamanho</c>) e ignora a caixa, então um erro em <c>tamanho</c> sairia com a chave antiga.
/// Erros de binding (JSON malformado, tipo errado) chegam antes, pelo filtro automático do [ApiController].
/// </remarks>
internal sealed class ValidacaoFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contexto, ActionExecutionDelegate proximo)
    {
        var erros = new ModelStateDictionary();
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
                erros.AddModelError(JsonNamingPolicy.CamelCase.ConvertName(erro.PropertyName), erro.ErrorMessage);
            }
        }

        if (erros.ErrorCount > 0 && contexto.Controller is ControllerBase controller)
        {
            contexto.Result = controller.ValidationProblem(erros);
            return;
        }

        await proximo();
    }
}
