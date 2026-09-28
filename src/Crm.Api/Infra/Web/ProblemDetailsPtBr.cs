using Microsoft.AspNetCore.WebUtilities;

namespace Crm.Api.Infra.Web;

/// <summary>
/// Respostas de erro no padrão ProblemDetails, com os títulos em português. Títulos definidos pelo código
/// (por exemplo, <c>Problem(title: ...)</c>) são mantidos; só os padrões do ASP.NET Core são traduzidos.
/// </summary>
internal static class ProblemDetailsPtBr
{
    private static readonly Dictionary<int, string> Titulos = new()
    {
        [StatusCodes.Status400BadRequest] = "Requisição inválida",
        [StatusCodes.Status401Unauthorized] = "Não autenticado",
        [StatusCodes.Status403Forbidden] = "Acesso negado",
        [StatusCodes.Status404NotFound] = "Recurso não encontrado",
        [StatusCodes.Status405MethodNotAllowed] = "Método não permitido",
        [StatusCodes.Status409Conflict] = "Conflito",
        [StatusCodes.Status415UnsupportedMediaType] = "Tipo de conteúdo não suportado",
        [StatusCodes.Status422UnprocessableEntity] = "Dados inválidos",
        [StatusCodes.Status429TooManyRequests] = "Muitas requisições",
        [StatusCodes.Status500InternalServerError] = "Erro interno",
        [StatusCodes.Status503ServiceUnavailable] = "Serviço indisponível",
    };

    // Títulos que o ASP.NET Core usa além da frase padrão de cada status.
    private static readonly HashSet<string> TitulosPadraoEmIngles =
    [
        "An error occurred while processing your request.",
        "One or more validation errors occurred.",
    ];

    public static IServiceCollection AddProblemDetailsPtBr(this IServiceCollection services) =>
        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            var problema = contexto.ProblemDetails;
            var status = problema.Status ?? contexto.HttpContext.Response.StatusCode;
            problema.Title = TituloEmPortugues(status, problema.Title);
        });

    /// <summary>Devolve o título em português quando <paramref name="tituloAtual"/> é o padrão em inglês.</summary>
    public static string? TituloEmPortugues(int status, string? tituloAtual)
    {
        var ehPadrao = tituloAtual is null
            || tituloAtual == ReasonPhrases.GetReasonPhrase(status)
            || TitulosPadraoEmIngles.Contains(tituloAtual);

        return ehPadrao && Titulos.TryGetValue(status, out var titulo) ? titulo : tituloAtual;
    }
}
