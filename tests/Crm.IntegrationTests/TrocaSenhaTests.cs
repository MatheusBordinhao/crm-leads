using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

public class TrocaSenhaTests(CrmApiFactory fabrica)
{
    private const string NovaSenha = "nova-senha-definitiva";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> TrocarSenhaAsync(HttpClient cliente, string senhaAtual, string novaSenha) =>
        cliente.PostAsJsonAsync("/api/auth/trocar-senha", new { senhaAtual, novaSenha }, Ct);

    [Fact]
    public async Task Com_senha_provisoria_o_login_avisa_que_precisa_trocar()
    {
        var usuario = await fabrica.CriarUsuarioAsync(trocarSenha: true);
        using var cliente = fabrica.CriarClienteHttps();

        var login = await cliente.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True((await login.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());
        var eu = await cliente.GetAsync("/api/auth/eu", Ct);
        Assert.True((await eu.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());
    }

    [Fact]
    public async Task Com_senha_provisoria_as_outras_rotas_respondem_403_ate_trocar()
    {
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin, trocarSenha: true);
        using var cliente = await fabrica.LogarAsync(admin);

        var antes = await cliente.GetAsync("/api/usuarios", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.Equal(SenhaDefinidaRequirement.Mensagem, await antes.LerDetalheAsync());

        await TrocarSenhaAsync(cliente, UsuariosDeTeste.SenhaPadrao, NovaSenha);

        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/usuarios", Ct)).StatusCode);
    }

    [Fact]
    public async Task Trocar_a_senha_libera_o_acesso_e_a_senha_antiga_para_de_valer()
    {
        var usuario = await fabrica.CriarUsuarioAsync(trocarSenha: true);
        using var cliente = await fabrica.LogarAsync(usuario);

        var troca = await TrocarSenhaAsync(cliente, UsuariosDeTeste.SenhaPadrao, NovaSenha);

        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);
        var eu = await cliente.GetAsync("/api/auth/eu", Ct);
        Assert.False((await eu.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());

        using var outro = fabrica.CriarClienteHttps();
        Assert.Equal(HttpStatusCode.Unauthorized, (await outro.PostLoginAsync(usuario.Email, UsuariosDeTeste.SenhaPadrao)).StatusCode);
        var comANova = await outro.PostLoginAsync(usuario.Email, NovaSenha);
        Assert.Equal(HttpStatusCode.OK, comANova.StatusCode);
        Assert.False((await comANova.LerJsonAsync()).GetProperty("trocarSenha").GetBoolean());
    }

    [Fact]
    public async Task Senha_atual_errada_responde_400_no_campo_senhaAtual()
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await TrocarSenhaAsync(cliente, "senha-errada-123", NovaSenha);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["Senha atual incorreta."], await resposta.LerErrosDoCampoAsync("senhaAtual"));
    }

    [Theory]
    [InlineData("curta", "A senha precisa ter pelo menos 10 caracteres.")]
    [InlineData(UsuariosDeTeste.SenhaPadrao, "A nova senha precisa ser diferente da atual.")]
    public async Task Nova_senha_invalida_responde_400_no_campo_novaSenha(string novaSenha, string erro)
    {
        using var cliente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await TrocarSenhaAsync(cliente, UsuariosDeTeste.SenhaPadrao, novaSenha);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal([erro], await resposta.LerErrosDoCampoAsync("novaSenha"));
    }

    [Fact]
    public async Task Sem_sessao_nao_troca_a_senha()
    {
        using var cliente = fabrica.CriarClienteHttps();

        var resposta = await TrocarSenhaAsync(cliente, UsuariosDeTeste.SenhaPadrao, NovaSenha);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }
}
