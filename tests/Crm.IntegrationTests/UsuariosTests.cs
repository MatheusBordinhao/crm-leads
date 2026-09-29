using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

public class UsuariosTests(CrmApiFactory fabrica)
{
    private const string SenhaProvisoria = "provisoria-123";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string EmailNovo() => $"novo-{Guid.NewGuid():N}@example.com";

    private static Task<HttpResponseMessage> CriarAsync(
        HttpClient admin, string email, string perfil = "Atendente", string nome = "Adriana", string senha = SenhaProvisoria) =>
        admin.PostAsJsonAsync(
            "/api/usuarios",
            new { nome, email, perfil, participaRodizio = true, senhaProvisoria = senha },
            Ct);

    private static Task<HttpResponseMessage> EditarAsync(
        HttpClient admin, Guid id, string nome, string email, string perfil, bool ativo, bool participaRodizio) =>
        admin.PutAsJsonAsync($"/api/usuarios/{id}", new { nome, email, perfil, ativo, participaRodizio }, Ct);

    private static Task<HttpResponseMessage> EditarAsync(HttpClient admin, Usuario usuario, string perfil = "Atendente", bool ativo = true) =>
        EditarAsync(admin, usuario.Id, usuario.Nome, usuario.Email!, perfil, ativo, usuario.ParticipaRodizio);

    [Fact]
    public async Task Admin_cria_usuario_que_entra_com_a_senha_provisoria_e_precisa_troca_la()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var email = EmailNovo();

        var resposta = await CriarAsync(admin, $"  {email.ToUpperInvariant()} ", nome: "  Adriana  ");

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.LerJsonAsync();
        var id = criado.GetProperty("id").GetGuid();
        Assert.Equal($"/api/usuarios/{id}", resposta.Headers.Location?.AbsolutePath);
        Assert.Equal("Adriana", criado.GetProperty("nome").GetString());
        Assert.Equal(email, criado.GetProperty("email").GetString());
        Assert.Equal("Atendente", criado.GetProperty("perfil").GetString());
        Assert.True(criado.GetProperty("ativo").GetBoolean());
        Assert.True(criado.GetProperty("participaRodizio").GetBoolean());
        Assert.True(criado.GetProperty("trocarSenha").GetBoolean());

        using var novo = fabrica.CriarClienteHttps();
        var login = await novo.PostLoginAsync(email, SenhaProvisoria);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());
    }

    [Fact]
    public async Task Email_ja_usado_responde_400_no_campo_email()
    {
        var existente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await CriarAsync(admin, existente.Email!.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Este e-mail já está em uso."], await resposta.LerErrosDoCampoAsync("email"));
    }

    [Fact]
    public async Task Dados_invalidos_respondem_400_com_a_mensagem_em_cada_campo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await CriarAsync(admin, "sem-arroba", nome: " ", senha: "curta");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Informe o nome."], await resposta.LerErrosDoCampoAsync("nome"));
        Assert.Equal(["E-mail inválido."], await resposta.LerErrosDoCampoAsync("email"));
        Assert.Equal(["A senha precisa ter pelo menos 10 caracteres."], await resposta.LerErrosDoCampoAsync("senhaProvisoria"));
    }

    [Fact]
    public async Task Perfil_ausente_responde_400_em_vez_de_virar_o_perfil_padrao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { nome = "Sem perfil", email = EmailNovo(), participaRodizio = false, senhaProvisoria = SenhaProvisoria },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Perfil_enviado_como_zero_responde_400_e_nao_cria_admin()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var email = EmailNovo();

        var resposta = await admin.PostAsJsonAsync(
            "/api/usuarios",
            new { nome = "Perfil zero", email, perfil = 0, participaRodizio = false, senhaProvisoria = SenhaProvisoria },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Perfil inválido."], await resposta.LerErrosDoCampoAsync("perfil"));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.Null(await escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>().FindByEmailAsync(email));
    }

    [Fact]
    public async Task Editar_com_perfil_zero_responde_400_e_mantem_o_perfil()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            new { nome = usuario.Nome, email = usuario.Email, perfil = 0, ativo = true, participaRodizio = false },
            Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Perfil inválido."], await resposta.LerErrosDoCampoAsync("perfil"));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        Assert.Equal([nameof(Perfil.Atendente)], await usuarios.GetRolesAsync((await usuarios.FindByIdAsync(usuario.Id.ToString()))!));
    }

    [Fact]
    public async Task Lista_mostra_perfil_e_bloqueio()
    {
        var atendente = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var salvo = await usuarios.FindByIdAsync(atendente.Id.ToString());
            await usuarios.SetLockoutEndDateAsync(salvo!, DateTimeOffset.UtcNow.AddMinutes(10));
        }

        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var lista = (await (await admin.GetAsync("/api/usuarios", Ct)).LerJsonAsync()).EnumerateArray().ToList();

        var linhaAtendente = lista.Single(u => u.GetProperty("id").GetGuid() == atendente.Id);
        var linhaAdmin = lista.Single(u => u.GetProperty("id").GetGuid() == outroAdmin.Id);
        Assert.Equal("Atendente", linhaAtendente.GetProperty("perfil").GetString());
        Assert.Equal("Admin", linhaAdmin.GetProperty("perfil").GetString());
        Assert.Equal(JsonValueKind.String, linhaAtendente.GetProperty("bloqueadoAte").ValueKind);
        Assert.Equal(JsonValueKind.Null, linhaAdmin.GetProperty("bloqueadoAte").ValueKind);
    }

    [Fact]
    public async Task Usuario_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.GetAsync($"/api/usuarios/{Guid.NewGuid()}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Editar_muda_nome_email_perfil_e_rodizio()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var email = EmailNovo();

        var resposta = await EditarAsync(admin, usuario.Id, "Pietra", email, "Admin", ativo: true, participaRodizio: true);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var editado = await resposta.LerJsonAsync();
        Assert.Equal("Pietra", editado.GetProperty("nome").GetString());
        Assert.Equal(email, editado.GetProperty("email").GetString());
        Assert.Equal("Admin", editado.GetProperty("perfil").GetString());
        Assert.True(editado.GetProperty("participaRodizio").GetBoolean());

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var salvo = await usuarios.FindByEmailAsync(email);
        Assert.Equal([nameof(Perfil.Admin)], await usuarios.GetRolesAsync(salvo!));
    }

    [Fact]
    public async Task Usuario_desativado_pelo_admin_nao_entra_mais()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await EditarAsync(admin, usuario, ativo: false);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.False((await resposta.LerJsonAsync()).GetProperty("ativo").GetBoolean());
        using var cliente = fabrica.CriarClienteHttps();
        var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);
        Assert.Equal(AuthController.MensagemDesativado, await login.LerDetalheAsync());
    }

    [Theory]
    [InlineData("Admin", false)]
    [InlineData("Atendente", true)]
    public async Task Admin_nao_pode_se_desativar_nem_tirar_o_proprio_perfil_de_admin(string perfil, bool ativo)
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);

        var resposta = await EditarAsync(admin, eu, perfil, ativo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(UsuariosController.MensagemAutoprotecao, await resposta.LerDetalheAsync());
        var salvo = await fabrica.BuscarUsuarioAsync(eu.Id);
        Assert.True(salvo.Ativo);
    }

    [Fact]
    public async Task Redefinir_senha_desbloqueia_e_exige_a_troca()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var cliente = fabrica.CriarClienteHttps();
        for (var tentativa = 1; tentativa <= 5; tentativa++)
        {
            await cliente.PostLoginAsync(usuario.Email, "senha-errada-123");
        }

        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PostAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/redefinir-senha", new { senhaProvisoria = SenhaProvisoria }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        var antiga = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);
        Assert.Equal(AuthController.MensagemCredenciaisInvalidas, await antiga.LerDetalheAsync());
        var nova = await cliente.PostLoginAsync(usuario.Email, SenhaProvisoria);
        Assert.Equal(HttpStatusCode.OK, nova.StatusCode);
        Assert.True((await nova.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());
    }

    [Fact]
    public async Task Redefinir_com_senha_curta_responde_400()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await admin.PostAsJsonAsync(
            $"/api/usuarios/{usuario.Id}/redefinir-senha", new { senhaProvisoria = "curta" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["A senha precisa ter pelo menos 10 caracteres."], await resposta.LerErrosDoCampoAsync("senhaProvisoria"));
    }
}
