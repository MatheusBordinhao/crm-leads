using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Crm.IntegrationTests.Infra;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

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

    /// <summary>
    /// O X-Api-Key também em minúsculas, como chega pelo HTTP/2 e de alguns clientes: o Caddy, em Go, põe o nome na forma
    /// canônica antes do filtro do log.
    /// </summary>
    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData("x-api-key")]
    public async Task O_502_com_a_API_fora_do_ar_vai_para_o_log_sem_a_query_string_o_Referer_e_o_X_Api_Key(string nomeDaChave)
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

        await ConferirO502SemDadosNoLogAsync(caddy, nomeDaChave);
    }

    /// <summary>
    /// O mesmo log no Caddy da publicação no Coolify (deploy/Caddyfile.coolify), atrás do Traefik: nada escuta na 8080 dentro
    /// do próprio contêiner, e o proxy recebe a conexão recusada, como com a API reiniciando.
    /// </summary>
    [Theory]
    [InlineData("X-Api-Key")]
    [InlineData("x-api-key")]
    public async Task No_Coolify_o_502_vai_para_o_log_sem_a_query_string_o_Referer_e_o_X_Api_Key(string nomeDaChave)
    {
        await using var caddy = CaddyDoCoolify(apiEndereco: "127.0.0.1:8080");
        await caddy.StartAsync(Ct);

        await ConferirO502SemDadosNoLogAsync(caddy, nomeDaChave);
    }

    /// <summary>
    /// No Coolify, o Traefik chega ao Caddy por uma rede do Docker, numa faixa privada, e acrescenta à direita do
    /// X-Forwarded-For o IP de quem acessa. O Caddy lê da direita para a esquerda: o primeiro IP fora das faixas privadas é o
    /// de quem acessa, e o da esquerda, que pode ser forjado, não vale. A API recebe só esse IP e o esquema https. Aqui, o
    /// próprio teste faz o papel do Traefik, e um Caddy de eco, o da API.
    /// </summary>
    [Fact]
    public async Task No_Coolify_a_API_recebe_o_IP_mais_a_direita_fora_das_faixas_privadas_e_o_esquema_https()
    {
        await using var rede = new NetworkBuilder().Build();
        await rede.CreateAsync(Ct);
        var eco = "{\n\tadmin off\n}\n\n:8080 {\n\trespond \"{header.X-Forwarded-For}|{header.X-Forwarded-Proto}\"\n}\n"u8.ToArray();
        await using var api = new ContainerBuilder(Repositorio.ImagemDoComposeDeProducao("caddy"))
            .WithNetwork(rede)
            .WithNetworkAliases("eco")
            .WithResourceMapping(eco, "/etc/caddy/Caddyfile")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("serving initial configuration"))
            .Build();
        await api.StartAsync(Ct);
        await using var caddy = CaddyDoCoolify(apiEndereco: "eco:8080", rede);
        await caddy.StartAsync(Ct);
        using var http = ClienteDo(caddy);

        using var forjado = new HttpRequestMessage(HttpMethod.Get, "/");
        forjado.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.7, 198.51.100.20");
        using var resposta = await http.SendAsync(forjado, Ct);

        Assert.Equal("198.51.100.20|https", await resposta.Content.ReadAsStringAsync(Ct));
        Assert.Equal("max-age=31536000", Assert.Single(resposta.Headers.GetValues("Strict-Transport-Security")));
        Assert.Contains("connect-src 'self' wss://crm.example.com;", Assert.Single(resposta.Headers.GetValues("Content-Security-Policy")));
        Assert.False(resposta.Headers.Contains("Server"));

        // Sem X-Forwarded-For, vale o IP de quem conectou: o da rede do Docker.
        var semCabecalho = (await http.GetStringAsync("/", Ct)).Split('|');
        Assert.True(IPAddress.TryParse(semCabecalho[0], out _), string.Join('|', semCabecalho));
        Assert.Equal("https", semCabecalho[1]);
    }

    private static IContainer CaddyDoCoolify(string apiEndereco, INetwork? rede = null)
    {
        var caddyfile = File.ReadAllBytes(Path.Combine(Repositorio.Raiz, "deploy", "Caddyfile.coolify"));
        var construtor = new ContainerBuilder(Repositorio.ImagemDoComposeDeProducao("caddy"))
            .WithEnvironment("DOMINIO", "crm.example.com")
            .WithEnvironment("API_ENDERECO", apiEndereco)
            .WithResourceMapping(caddyfile, "/etc/caddy/Caddyfile")
            .WithPortBinding(80, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("serving initial configuration"));
        return (rede is null ? construtor : construtor.WithNetwork(rede)).Build();
    }

    private static HttpClient ClienteDo(IContainer caddy) =>
        new(new SocketsHttpHandler { UseCookies = false })
        {
            BaseAddress = new Uri($"http://{caddy.Hostname}:{caddy.GetMappedPublicPort(80)}"),
        };

    /// <summary>Um pedido com dados sigilosos na URL e nos cabeçalhos, com a API fora do ar, e o que o log guarda dele.</summary>
    private static async Task ConferirO502SemDadosNoLogAsync(IContainer caddy, string nomeDaChave)
    {
        using var http = ClienteDo(caddy);
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/leads/42?busca=maria.sigilosa%40example.com&pagina=2");
        requisicao.Headers.Referrer = new Uri("https://crm.example.com/leads?busca=referer.sigiloso%40example.com");
        requisicao.Headers.Add(nomeDaChave, "chave-sigilosa-da-integracao");
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
