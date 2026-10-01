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

    /// <summary>
    /// Os arquivos do build e, nos endereços do React Router (<c>/funil</c>, <c>/leads/12</c>), o index.html. A página
    /// fica fora do roteamento, como último recurso de um GET que nenhuma rota atendeu: uma rota que aceita qualquer
    /// método (como o <c>MapFallbackToFile</c>) faria um método errado numa rota da API responder 404 em vez de 405.
    /// Assim, um endereço de /api ou /hubs que não existe responde 404, e um método errado, 405, os dois em
    /// ProblemDetails. Precisa ficar depois do roteamento, que o WebApplication põe no início.
    /// </summary>
    public static WebApplication UsarFront(this WebApplication app)
    {
        var opcoes = new StaticFileOptions { OnPrepareResponse = DefinirCache };
        app.UseStaticFiles(opcoes);
        app.UseWhen(EnderecoDoFront, pagina => pagina
            .Use((contexto, proximo) =>
            {
                contexto.Request.Path = "/index.html";
                return proximo(contexto);
            })
            .UseStaticFiles(opcoes));
        return app;
    }

    /// <summary>Um GET que nenhuma rota atendeu, fora de /api e /hubs, num endereço que não é de arquivo.</summary>
    private static bool EnderecoDoFront(HttpContext contexto)
    {
        var requisicao = contexto.Request;
        return contexto.GetEndpoint() is null
            && (HttpMethods.IsGet(requisicao.Method) || HttpMethods.IsHead(requisicao.Method))
            && !requisicao.Path.StartsWithSegments("/api")
            && !requisicao.Path.StartsWithSegments("/hubs")
            && !Path.HasExtension(requisicao.Path.Value);
    }

    private static void DefinirCache(StaticFileResponseContext contexto) =>
        contexto.Context.Response.Headers.CacheControl =
            contexto.Context.Request.Path.StartsWithSegments("/assets") ? CacheLongo : SemCache;
}
