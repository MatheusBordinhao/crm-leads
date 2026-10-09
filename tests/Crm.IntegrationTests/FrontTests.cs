using System.Net;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;

namespace Crm.IntegrationTests;

/// <summary>A API serve o build do React (Passo 5), aqui num wwwroot de mentira, criado pelo teste.</summary>
public class FrontTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Rotas_do_front_devolvem_o_index_sem_cache_e_os_assets_tem_cache_longo()
    {
        var pasta = Directory.CreateTempSubdirectory("crm-front-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(pasta.FullName, "index.html"), "<!doctype html><title>CRM de Leads</title>", Ct);
            Directory.CreateDirectory(Path.Combine(pasta.FullName, "assets"));
            await File.WriteAllTextAsync(Path.Combine(pasta.FullName, "assets", "index-abc123.js"), "console.log('crm')", Ct);
            using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.WebRootKey, pasta.FullName));
            using var cliente = api.CriarClienteHttps();

            foreach (var rota in new[] { "/", "/funil", "/leads/1024", "/importacao/12" })
            {
                var pagina = await cliente.GetAsync(rota, Ct);
                Assert.True(pagina.StatusCode == HttpStatusCode.OK, $"{rota} respondeu {(int)pagina.StatusCode}");
                Assert.Equal("text/html", pagina.Content.Headers.ContentType?.MediaType);
                Assert.Contains("<title>CRM de Leads</title>", await pagina.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
                Assert.True(pagina.Headers.CacheControl?.NoCache, $"{rota} sem o no-cache");
            }

            var asset = await cliente.GetAsync("/assets/index-abc123.js", Ct);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal("public, max-age=31536000, immutable", asset.Headers.CacheControl?.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/assets/nao-existe.js", Ct)).StatusCode);
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Endereco_da_API_ou_do_hub_que_nao_existe_responde_404_em_problem_details_e_nao_com_o_front()
    {
        var pasta = Directory.CreateTempSubdirectory("crm-front-");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(pasta.FullName, "index.html"), "<!doctype html><title>CRM de Leads</title>", Ct);
            using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting(WebHostDefaults.WebRootKey, pasta.FullName));
            using var cliente = api.CriarClienteHttps();

            var casos = new (HttpMethod Metodo, string Rota, HttpStatusCode Esperado)[]
            {
                (HttpMethod.Get, "/api/nao-existe", HttpStatusCode.NotFound),
                (HttpMethod.Get, "/API/nao-existe", HttpStatusCode.NotFound),
                (HttpMethod.Get, "/api", HttpStatusCode.NotFound),
                (HttpMethod.Get, "/api/leads/abc", HttpStatusCode.NotFound),
                (HttpMethod.Get, "/hubs/nao-existe", HttpStatusCode.NotFound),
                (HttpMethod.Post, "/api/nao-existe", HttpStatusCode.NotFound),

                // Uma rota que existe, com o método errado, continua respondendo 405, como sem o front.
                (HttpMethod.Patch, "/api/leads/1", HttpStatusCode.MethodNotAllowed),
                (HttpMethod.Get, "/api/auth/login", HttpStatusCode.MethodNotAllowed),
            };

            foreach (var (metodo, rota, esperado) in casos)
            {
                using var requisicao = new HttpRequestMessage(metodo, rota);
                var resposta = await cliente.SendAsync(requisicao, Ct);
                Assert.True(resposta.StatusCode == esperado, $"{metodo} {rota} respondeu {(int)resposta.StatusCode}");
                Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
            }

            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/apis-e-hubs", Ct)).StatusCode);
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Sem_o_build_do_front_como_em_desenvolvimento_as_rotas_do_front_respondem_404()
    {
        using var cliente = fabrica.CriarClienteHttps();

        var resposta = await cliente.GetAsync("/funil", Ct);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
    }
}
