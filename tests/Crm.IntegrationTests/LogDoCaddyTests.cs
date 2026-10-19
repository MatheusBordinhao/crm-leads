using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Crm.IntegrationTests.Infra;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Crm.IntegrationTests;

/// <summary>
/// O log do Caddy de produção (Passo 5), com o Caddyfile e a imagem do docker-compose.prod.yml. O log de erro fica ligado e
/// grava cada falha do proxy com a URL e os cabeçalhos da requisição, mas sem a query string, que pode levar a busca da
/// lista, sem o Referer e sem o X-Api-Key. O Cookie e o Authorization o próprio Caddy já grava como REDACTED.
/// </summary>
public class LogDoCaddyTests
{
    private const string LoggerDoErro = "\"logger\":\"http.log.error\"";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task O_502_com_a_API_fora_do_ar_vai_para_o_log_sem_a_query_string_o_Referer_e_o_X_Api_Key()
    {
        var caddyfile = await File.ReadAllBytesAsync(Path.Combine(Repositorio.Raiz, "deploy", "Caddyfile"), Ct);
        await using var rede = new NetworkBuilder().Build();
        await rede.CreateAsync(Ct);

        // Na rede do teste, o nome "api" é o próprio Caddy, que não escuta na 8080: o proxy recebe a conexão recusada, como
        // com a API reiniciando. Com DOMINIO=:80, o site é servido em HTTP, sem certificado, e o log é o mesmo.
        await using var caddy = new ContainerBuilder(Repositorio.ImagemDoComposeDeProducao("caddy"))
            .WithNetwork(rede)
            .WithNetworkAliases("api")
            .WithEnvironment("DOMINIO", ":80")
            .WithResourceMapping(caddyfile, "/etc/caddy/Caddyfile")
            .WithPortBinding(80, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("serving initial configuration"))
            .Build();
        await caddy.StartAsync(Ct);

        using var http = new HttpClient(new SocketsHttpHandler { UseCookies = false })
        {
            BaseAddress = new Uri($"http://{caddy.Hostname}:{caddy.GetMappedPublicPort(80)}"),
        };
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/leads/42?busca=maria.sigilosa%40example.com&pagina=2");
        requisicao.Headers.Referrer = new Uri("https://crm.example.com/leads?busca=referer.sigiloso%40example.com");
        requisicao.Headers.Add("X-Api-Key", "chave-sigilosa-da-integracao");
        requisicao.Headers.Add("Cookie", ".AspNetCore.Identity.Application=cookie-sigiloso");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-sigiloso");

        using var resposta = await http.SendAsync(requisicao, Ct);

        Assert.Equal(HttpStatusCode.BadGateway, resposta.StatusCode);
        var log = await LogComOErroAsync(caddy);
        using var erro = JsonDocument.Parse(Assert.Single(log.Split('\n'), l => l.Contains(LoggerDoErro, StringComparison.Ordinal)));
        Assert.Equal(502, erro.RootElement.GetProperty("status").GetInt32());
        var pedido = erro.RootElement.GetProperty("request");
        Assert.Equal("/api/leads/42?REDACTED", pedido.GetProperty("uri").GetString());
        var cabecalhos = pedido.GetProperty("headers");
        Assert.Equal("REDACTED", cabecalhos.GetProperty("Referer").GetString());
        Assert.Equal("REDACTED", cabecalhos.GetProperty("X-Api-Key").GetString());
        Assert.Equal("REDACTED", Assert.Single(cabecalhos.GetProperty("Cookie").EnumerateArray()).GetString());
        Assert.Equal("REDACTED", Assert.Single(cabecalhos.GetProperty("Authorization").EnumerateArray()).GetString());
        string[] sigilosos = ["maria.sigilosa", "referer.sigiloso", "chave-sigilosa", "cookie-sigiloso", "token-sigiloso", "busca="];
        Assert.All(sigilosos, sigiloso => Assert.DoesNotContain(sigiloso, log, StringComparison.Ordinal));
    }

    /// <summary>
    /// O log do contêiner, depois que a entrada do log de erro chega: o Caddy a grava antes de responder, mas o Docker
    /// recolhe o stderr com um pequeno atraso.
    /// </summary>
    private static async Task<string> LogComOErroAsync(IContainer caddy)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var (saida, erros) = await caddy.GetLogsAsync(timestampsEnabled: false, ct: Ct);
            if (erros.Contains(LoggerDoErro, StringComparison.Ordinal) || tentativa == 50)
            {
                return saida + erros;
            }

            await Task.Delay(100, Ct);
        }
    }
}
