using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

public class AutorizacaoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IEnumerable<RouteEndpoint> EndpointsDosControllers() =>
        fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not null);

    /// <summary>As rotas que passam no filtro, inclusive as que os próximos passos criarem.</summary>
    private List<(string Metodo, string Caminho)> Rotas(Func<RouteEndpoint, bool> filtro) =>
        [.. EndpointsDosControllers()
            .Where(filtro)
            .Select(e => (e.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Single(), MontarCaminho(e.RoutePattern)))];

    private List<(string Metodo, string Caminho)> RotasDeAdmin() => Rotas(e => TemPolitica(e, Politicas.Admin));

    private static bool TemPolitica(RouteEndpoint endpoint, string politica) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == politica);

    private static string MontarCaminho(RoutePattern padrao) =>
        "/" + string.Join("/", padrao.PathSegments.Select(segmento => string.Concat(segmento.Parts.Select(parte => parte switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separador => separador.Content,
            RoutePatternParameterPart parametro when parametro.ParameterPolicies.Any(p => p.Content == "guid") =>
                Guid.NewGuid().ToString(),
            _ => "1",
        }))));

    private static async Task<HttpResponseMessage> ChamarAsync(HttpClient cliente, string metodo, string caminho)
    {
        using var requisicao = new HttpRequestMessage(new HttpMethod(metodo), caminho);
        if (metodo is "POST" or "PUT" or "PATCH")
        {
            requisicao.Content = JsonContent.Create(new { });
        }

        return await cliente.SendAsync(requisicao, Ct);
    }

    [Fact]
    public async Task Toda_rota_de_admin_responde_403_para_atendente()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var rotas = RotasDeAdmin();

        Assert.NotEmpty(rotas);
        foreach (var (metodo, caminho) in rotas)
        {
            var resposta = await ChamarAsync(atendente, metodo, caminho);
            Assert.True(
                resposta.StatusCode == HttpStatusCode.Forbidden,
                $"{metodo} {caminho} respondeu {(int)resposta.StatusCode} para a atendente");
        }
    }

    [Fact]
    public async Task Toda_rota_de_admin_responde_401_sem_sessao()
    {
        using var anonimo = fabrica.CriarClienteHttps();

        foreach (var (metodo, caminho) in RotasDeAdmin())
        {
            var resposta = await ChamarAsync(anonimo, metodo, caminho);
            Assert.True(
                resposta.StatusCode == HttpStatusCode.Unauthorized,
                $"{metodo} {caminho} respondeu {(int)resposta.StatusCode} sem sessão");
        }
    }

    [Fact]
    public async Task O_403_da_atendente_vem_em_problem_details()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync("/api/usuarios", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal("Acesso negado", (await resposta.LerProblemaAsync()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Toda_rota_da_integracao_fica_em_api_integracoes_e_nao_aceita_a_sessao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var rotas = Rotas(e => TemPolitica(e, Politicas.Integracao));

        Assert.NotEmpty(rotas);
        foreach (var (metodo, caminho) in rotas)
        {
            Assert.StartsWith("/api/integracoes/", caminho, StringComparison.Ordinal);
            var resposta = await ChamarAsync(admin, metodo, caminho);
            Assert.True(
                resposta.StatusCode == HttpStatusCode.Unauthorized,
                $"{metodo} {caminho} respondeu {(int)resposta.StatusCode} com a sessão de admin e sem a chave");
        }
    }

    [Fact]
    public async Task A_chave_de_integracao_nao_vale_fora_das_rotas_da_integracao()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var rotas = Rotas(e => !TemPolitica(e, Politicas.Integracao) && e.Metadata.GetMetadata<IAllowAnonymous>() is null);

        Assert.NotEmpty(rotas);
        foreach (var (metodo, caminho) in rotas)
        {
            var resposta = await ChamarAsync(n8n, metodo, caminho);
            Assert.True(
                resposta.StatusCode == HttpStatusCode.Unauthorized,
                $"{metodo} {caminho} respondeu {(int)resposta.StatusCode} só com a chave de integração");
        }
    }

    [Fact]
    public void Todo_endpoint_de_controller_declara_a_autorizacao()
    {
        var semDeclaracao = EndpointsDosControllers()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null && !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(e => e.DisplayName)
            .ToList();

        Assert.NotEmpty(EndpointsDosControllers());
        Assert.Empty(semDeclaracao);
    }

    [Fact]
    public void So_o_login_e_anonimo()
    {
        var anonimos = EndpointsDosControllers()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => $"{e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single()} /{e.RoutePattern.RawText}");

        Assert.Equal(["POST /api/auth/login"], anonimos);
    }
}
