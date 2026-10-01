using System.Net;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.IntegrationTests;

/// <summary>
/// As chaves que protegem o cookie da sessão ficam numa pasta (em produção, um volume): reiniciar a API não encerra as
/// sessões (Passo 5).
/// </summary>
public class ChavesDaSessaoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private WebApplicationFactory<Program> ApiComAsChavesEm(string pasta) =>
        fabrica.WithWebHostBuilder(builder => builder.UseSetting("DataProtection:Pasta", pasta));

    private static async Task<HttpStatusCode> EuComOCookieAsync(WebApplicationFactory<Program> api, string cookie)
    {
        using var cliente = api.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/auth/eu");
        requisicao.Headers.Add("Cookie", cookie);
        return (await cliente.SendAsync(requisicao, Ct)).StatusCode;
    }

    [Fact]
    public async Task A_sessao_continua_valendo_quando_a_API_reinicia_com_as_mesmas_chaves()
    {
        var pasta = Directory.CreateTempSubdirectory("crm-chaves-");
        var outraPasta = Directory.CreateTempSubdirectory("crm-chaves-");
        try
        {
            var usuario = await fabrica.CriarUsuarioAsync();
            string cookie;
            using (var antes = ApiComAsChavesEm(pasta.FullName))
            {
                using var cliente = antes.CriarClienteHttps();
                var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);
                login.EnsureSuccessStatusCode();
                cookie = login.Headers.GetValues("Set-Cookie")
                    .Single(c => c.StartsWith($"{AcessoExtensions.NomeCookie}=", StringComparison.Ordinal))
                    .Split(';')[0];
            }

            // A API "reinicia": outra instância, que lê as chaves da mesma pasta. Com outra pasta, o cookie não vale.
            using var depois = ApiComAsChavesEm(pasta.FullName);
            using var semAsChaves = ApiComAsChavesEm(outraPasta.FullName);

            Assert.NotEmpty(pasta.GetFiles("*.xml"));
            Assert.Equal(HttpStatusCode.OK, await EuComOCookieAsync(depois, cookie));
            Assert.Equal(HttpStatusCode.Unauthorized, await EuComOCookieAsync(semAsChaves, cookie));
        }
        finally
        {
            pasta.Delete(recursive: true);
            outraPasta.Delete(recursive: true);
        }
    }
}
