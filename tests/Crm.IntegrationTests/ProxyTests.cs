using System.Net;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.IntegrationTests;

/// <summary>
/// IP real de quem acessa, atrás do Caddy (Passo 5). No TestServer, a conexão não tem IP: cada requisição define o seu,
/// como chegaria do Caddy ou de outro lugar.
/// </summary>
public class ProxyTests(CrmApiFactory fabrica)
{
    private const string IpDoCaddy = "10.0.0.2";

    private const string IpDoCliente = "203.0.113.7";

    private WebApplicationFactory<Program> ApiAtrasDoCaddy(int loginsPorMinuto = 100_000) =>
        fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Proxy:Ip", IpDoCaddy);
            builder.UseSetting("Acesso:LoginsPorMinuto", loginsPorMinuto.ToString());
        });

    /// <summary>
    /// Um login que chega à API pela conexão de <paramref name="ipDaConexao"/>. O Caddy fala com a API em http e manda o
    /// IP do cliente no <c>X-Forwarded-For</c>.
    /// </summary>
    private static Task<HttpContext> LoginAsync(
        WebApplicationFactory<Program> api, string ipDaConexao, string? xForwardedFor, string email, string senha) =>
        api.Server.SendAsync(
            contexto =>
            {
                contexto.Connection.RemoteIpAddress = IPAddress.Parse(ipDaConexao);
                contexto.Request.Method = HttpMethods.Post;
                contexto.Request.Scheme = "http";
                contexto.Request.Host = new HostString("crm.teste");
                contexto.Request.Path = "/api/auth/login";
                if (xForwardedFor is not null)
                {
                    contexto.Request.Headers["X-Forwarded-For"] = xForwardedFor;
                    contexto.Request.Headers["X-Forwarded-Proto"] = "https";
                }

                contexto.Request.ContentType = "application/json";
                contexto.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new { email, senha }));
            },
            TestContext.Current.CancellationToken);

    private async Task<string?> IpDoUltimoLoginAsync(Usuario usuario) =>
        (await fabrica.AuditoriaAsync(EntidadesAuditoria.Usuario, usuario.Id.ToString())).Last(r => r.Acao == AcoesAuditoria.LoginSucesso).Ip;

    [Fact]
    public async Task Login_pelo_Caddy_grava_na_auditoria_o_IP_de_quem_acessa()
    {
        using var api = ApiAtrasDoCaddy();
        var usuario = await api.CriarUsuarioAsync();

        var login = await LoginAsync(api, IpDoCaddy, IpDoCliente, usuario.Email!, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(StatusCodes.Status200OK, login.Response.StatusCode);
        Assert.Equal(IpDoCliente, await IpDoUltimoLoginAsync(usuario));
    }

    [Fact]
    public async Task X_Forwarded_For_de_quem_nao_e_o_Caddy_e_ignorado()
    {
        using var api = ApiAtrasDoCaddy();
        var usuario = await api.CriarUsuarioAsync();

        var login = await LoginAsync(api, "198.51.100.9", "192.0.2.55", usuario.Email!, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(StatusCodes.Status200OK, login.Response.StatusCode);
        Assert.Equal("198.51.100.9", await IpDoUltimoLoginAsync(usuario));
    }

    [Fact]
    public async Task Sem_Proxy__Ip_a_API_nao_confia_em_nenhum_proxy()
    {
        var usuario = await fabrica.CriarUsuarioAsync();

        var login = await LoginAsync(fabrica, IpDoCaddy, IpDoCliente, usuario.Email!, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(StatusCodes.Status200OK, login.Response.StatusCode);
        Assert.Equal(IpDoCaddy, await IpDoUltimoLoginAsync(usuario));
    }

    [Fact]
    public async Task O_limite_de_logins_e_por_IP_de_quem_acessa_e_nao_do_Caddy()
    {
        // Fábrica à parte, com um contador só dela e um limite pequeno.
        using var api = ApiAtrasDoCaddy(loginsPorMinuto: 2);

        var primeiro = await LoginAsync(api, IpDoCaddy, IpDoCliente, "ninguem@example.com", "senha-errada-123");
        var segundo = await LoginAsync(api, IpDoCaddy, IpDoCliente, "ninguem@example.com", "senha-errada-123");
        var terceiro = await LoginAsync(api, IpDoCaddy, IpDoCliente, "ninguem@example.com", "senha-errada-123");
        var deOutroCliente = await LoginAsync(api, IpDoCaddy, "198.51.100.20", "ninguem@example.com", "senha-errada-123");

        Assert.Equal(StatusCodes.Status401Unauthorized, primeiro.Response.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, segundo.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, terceiro.Response.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, deOutroCliente.Response.StatusCode);
    }
}
