using System.Globalization;
using System.Net;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

public class LoginTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_com_a_senha_certa_abre_a_sessao()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var cliente = fabrica.CriarClienteHttps();

        var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var dados = await login.LerJsonAsync();
        Assert.Equal(usuario.Id, dados.GetProperty("id").GetGuid());
        Assert.Equal(usuario.Email, dados.GetProperty("email").GetString());
        Assert.Equal("Atendente", dados.GetProperty("perfil").GetString());
        Assert.False(dados.GetProperty("trocarSenha").GetBoolean());

        var eu = await cliente.GetAsync("/api/auth/eu", Ct);
        Assert.Equal(HttpStatusCode.OK, eu.StatusCode);
        Assert.Equal(usuario.Id, (await eu.LerJsonAsync()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cookie_da_sessao_e_HttpOnly_Secure_SameSite_Strict_e_vale_12_horas()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = fabrica.CriarClienteHttps();

        var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("crm_sessao=", StringComparison.Ordinal));
        var atributos = cookie.Split(';', StringSplitOptions.TrimEntries).Skip(1).ToList();
        Assert.Contains("httponly", atributos, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("secure", atributos, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", atributos, StringComparer.OrdinalIgnoreCase);
        var expira = DateTimeOffset.Parse(
            atributos.Single(a => a.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            CultureInfo.InvariantCulture);
        Assert.InRange(expira - DateTimeOffset.UtcNow, TimeSpan.FromHours(11.9), TimeSpan.FromHours(12.1));
    }

    [Fact]
    public async Task Senha_errada_e_email_desconhecido_recebem_a_mesma_resposta()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = fabrica.CriarClienteHttps();

        var senhaErrada = await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
        var emailDesconhecido = await cliente.PostLoginAsync("ninguem@example.com", UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.Unauthorized, senhaErrada.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, emailDesconhecido.StatusCode);
        Assert.Equal(AuthController.MensagemCredenciaisInvalidas, await senhaErrada.LerDetalheAsync());
        Assert.Equal(AuthController.MensagemCredenciaisInvalidas, await emailDesconhecido.LerDetalheAsync());
    }

    [Fact]
    public async Task Login_aceita_o_email_em_maiusculas_e_com_espacos()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = fabrica.CriarClienteHttps();

        var login = await cliente.PostLoginAsync($"  {usuario.Email!.ToUpperInvariant()} ", UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Usuario_desativado_nao_entra()
    {
        var usuario = await fabrica.CriarUsuarioAsync(ativo: false);
        using var cliente = fabrica.CriarClienteHttps();

        var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(AuthController.MensagemDesativado, await login.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu", Ct)).StatusCode);
    }

    [Fact]
    public async Task Sessao_de_usuario_desativado_deixa_de_valer()
    {
        // Revalida a sessão em toda requisição, em vez de a cada minuto.
        using var api = fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<SecurityStampValidatorOptions>(opcoes => opcoes.ValidationInterval = TimeSpan.Zero)));
        var usuario = await api.CriarUsuarioAsync();
        using var cliente = await api.LogarAsync(usuario);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/auth/eu", Ct)).StatusCode);

        await using (var escopo = api.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var salvo = await usuarios.FindByIdAsync(usuario.Id.ToString());
            salvo!.Ativo = false;
            await usuarios.UpdateAsync(salvo);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu", Ct)).StatusCode);
    }

    [Fact]
    public async Task Sexta_tentativa_errada_bloqueia_o_login()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = fabrica.CriarClienteHttps();

        // As 4 primeiras tentativas erradas só dizem que a senha está errada.
        for (var tentativa = 1; tentativa <= 4; tentativa++)
        {
            var resposta = await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
            Assert.Equal(AuthController.MensagemCredenciaisInvalidas, await resposta.LerDetalheAsync());
        }

        // A 5ª errada bloqueia a conta e já avisa; da 6ª em diante, o login é recusado, mesmo com a senha certa.
        var quinta = await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
        var sexta = await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
        var comASenhaCerta = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(AuthController.MensagemBloqueado, await quinta.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, sexta.StatusCode);
        Assert.Equal(AuthController.MensagemBloqueado, await sexta.LerDetalheAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, comASenhaCerta.StatusCode);
        Assert.Equal(AuthController.MensagemBloqueado, await comASenhaCerta.LerDetalheAsync());

        var bloqueado = await fabrica.BuscarUsuarioAsync(usuario.Id);
        Assert.InRange(bloqueado.LockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Logout_encerra_a_sessao()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var logout = await cliente.PostAsync("/api/auth/logout", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/api/auth/eu", Ct)).StatusCode);
    }

    [Fact]
    public async Task Sem_sessao_a_api_responde_401_em_problem_details()
    {
        using var cliente = fabrica.CriarClienteHttps();

        var resposta = await cliente.GetAsync("/api/auth/eu", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var problema = await resposta.LerProblemaAsync();
        Assert.Equal("Não autenticado", problema.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_sem_email_responde_400_com_o_erro_no_campo()
    {
        using var cliente = fabrica.CriarClienteHttps();

        var resposta = await cliente.PostLoginAsync(null, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Informe o e-mail."], await resposta.LerErrosDoCampoAsync("email"));
    }

    [Fact]
    public async Task O_11o_login_no_mesmo_minuto_responde_429()
    {
        // Fábrica à parte, com o limite padrão de 10 por minuto e um contador só dela.
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("Acesso:LoginsPorMinuto", "10"));
        using var cliente = api.CriarClienteHttps();

        for (var tentativa = 1; tentativa <= 10; tentativa++)
        {
            var resposta = await cliente.PostLoginAsync("ninguem@example.com", "senha-errada-123");
            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        var decimaPrimeira = await cliente.PostLoginAsync("ninguem@example.com", "senha-errada-123");

        Assert.Equal(HttpStatusCode.TooManyRequests, decimaPrimeira.StatusCode);
        Assert.NotNull(decimaPrimeira.Headers.RetryAfter);
        Assert.Equal("Muitas requisições", (await decimaPrimeira.LerProblemaAsync()).GetProperty("title").GetString());
    }
}
