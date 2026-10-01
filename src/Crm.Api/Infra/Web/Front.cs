using Microsoft.AspNetCore.StaticFiles;

namespace Crm.Api.Infra.Web;

/// <summary>
/// O build do React (<c>web/dist</c>), que em produção fica no wwwroot da API: um endereço só, sem CORS, com o cookie da
/// sessão na mesma origem. Em desenvolvimento, o Vite serve o front, e o wwwroot não existe.
/// </summary>
internal static class FrontExtensions
{
    /// <summary>Os arquivos de /assets levam um hash no nome, que muda quando o conteúdo muda.</summary>
    public const string CacheLongo = "public, max-age=31536000, immutable";

    /// <summary>O index.html é conferido a cada acesso, para a versão nova chegar assim que for publicada.</summary>
    public const string SemCache = "no-cache";

    public static WebApplication UsarArquivosDoFront(this WebApplication app)
    {
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = DefinirCache });
        return app;
    }

    /// <summary>
    /// As rotas do React Router (<c>/funil</c>, <c>/leads/12</c>) devolvem o index.html. Um endereço de /api ou /hubs
    /// que não existe responde 404 em ProblemDetails, e não com a página do front.
    /// </summary>
    public static WebApplication MapearFront(this WebApplication app)
    {
        app.MapFallback("/api/{**caminho}", NaoEncontrado);
        app.MapFallback("/hubs/{**caminho}", NaoEncontrado);
        app.MapFallbackToFile("index.html", new StaticFileOptions { OnPrepareResponse = DefinirCache });
        return app;
    }

    private static Task NaoEncontrado(HttpContext contexto)
    {
        contexto.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    private static void DefinirCache(StaticFileResponseContext contexto) =>
        contexto.Context.Response.Headers.CacheControl =
            contexto.Context.Request.Path.StartsWithSegments("/assets") ? CacheLongo : SemCache;
}
