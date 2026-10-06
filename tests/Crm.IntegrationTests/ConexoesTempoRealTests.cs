using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.TempoReal;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// O hub do tempo real (Passo 7): só abre com sessão e senha definitiva, põe cada pessoa nos grupos certos, com o perfil
/// lido no banco, e encerra a conexão quando a sessão deixa de valer.
/// </summary>
public class ConexoesTempoRealTests(CrmApiFactory fabrica)
{
    private const string Negociacao = $"{NotificacoesHub.Rota}/negotiate?negotiateVersion=1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IHubContext<NotificacoesHub, IClienteTempoReal> Hub =>
        fabrica.Services.GetRequiredService<IHubContext<NotificacoesHub, IClienteTempoReal>>();

    /// <summary>Um id que nenhum lead tem, para marcar um aviso mandado pelo próprio teste.</summary>
    private static long Marca() => -Random.Shared.NextInt64(1, long.MaxValue);

    [Fact]
    public void Os_enderecos_do_hub_exigem_sessao_com_a_senha_definitiva()
    {
        var enderecos = fabrica.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith(NotificacoesHub.Rota, StringComparison.Ordinal) == true)
            .ToList();

        // O da conexão e o da negociação.
        Assert.True(enderecos.Count >= 2, $"O hub tem {enderecos.Count} endereço(s).");
        Assert.All(enderecos, endereco =>
        {
            Assert.Null(endereco.Metadata.GetMetadata<IAllowAnonymous>());
            Assert.Contains(endereco.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy is null && a.Roles is null);
        });
    }

    [Fact]
    public async Task Negociacao_sem_sessao_responde_401()
    {
        using var cliente = fabrica.CriarClienteHttps();

        var resposta = await cliente.PostAsync(Negociacao, null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Negociacao_so_com_a_chave_de_integracao_responde_401()
    {
        using var n8n = fabrica.CriarClienteIntegracao();

        var resposta = await n8n.PostAsync(Negociacao, null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Negociacao_com_a_senha_provisoria_responde_403()
    {
        using var cliente = await fabrica.LogarAsync(await fabrica.CriarUsuarioAsync(trocarSenha: true));

        var resposta = await cliente.PostAsync(Negociacao, null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(SenhaDefinidaRequirement.Mensagem, await resposta.LerDetalheAsync());
    }

    [Fact]
    public async Task A_atendente_entra_no_proprio_grupo_e_nao_no_dos_admins()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        await using var conexao = await fabrica.ConectarAsync(atendente);
        var dosAdmins = Marca();
        var dela = Marca();

        await Hub.Clients.Group(GruposTempoReal.Admins).LeadsAlterados([dosAdmins]);
        await Hub.Clients.Group(GruposTempoReal.DoUsuario(atendente.Id)).LeadsAlterados([dela]);

        // As mensagens de uma conexão chegam em ordem: se o aviso dos admins tivesse chegado, viria antes.
        var antes = await conexao.AvisosAteAsync(a => a.Ids.Contains(dela));
        Assert.DoesNotContain(antes, a => a.Ids.Contains(dosAdmins));
    }

    [Fact]
    public async Task O_admin_entra_no_grupo_dos_admins_e_no_proprio()
    {
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        await using var conexao = await fabrica.ConectarAsync(admin);
        var dosAdmins = Marca();
        var dele = Marca();

        await Hub.Clients.Group(GruposTempoReal.Admins).LeadsAlterados([dosAdmins]);
        await Hub.Clients.Group(GruposTempoReal.DoUsuario(admin.Id)).LeadsAlterados([dele]);

        await conexao.AguardarAsync(a => a.Ids.Contains(dosAdmins));
        await conexao.AguardarAsync(a => a.Ids.Contains(dele));
    }

    [Fact]
    public async Task Conexao_de_quem_foi_desativado_depois_do_login_e_recusada()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        var cookie = await fabrica.CookieDaSessaoAsync(atendente);

        // O cookie só é conferido no banco a cada minuto; o hub confere ao conectar.
        await MudarUsuarioAsync(atendente.Id, usuario => usuario.Ativo = false);
        await using var conexao = await fabrica.TentarConectarComOCookieAsync(cookie);

        await conexao.AguardarFechamentoAsync();
    }

    [Fact]
    public async Task Conexao_com_um_cookie_de_antes_da_senha_redefinida_e_recusada()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        var cookie = await fabrica.CookieDaSessaoAsync(atendente);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            await usuarios.UpdateSecurityStampAsync((await usuarios.FindByIdAsync(atendente.Id.ToString()))!);
        }

        await using var conexao = await fabrica.TentarConectarComOCookieAsync(cookie);

        await conexao.AguardarFechamentoAsync();
    }

    [Fact]
    public async Task A_conferencia_encerra_a_conexao_de_quem_foi_desativado_e_mantem_as_outras()
    {
        var desativada = await fabrica.CriarUsuarioAsync();
        var ativa = await fabrica.CriarUsuarioAsync();
        await using var conexaoDesativada = await fabrica.ConectarAsync(desativada);
        await using var conexaoAtiva = await fabrica.ConectarAsync(ativa);

        await MudarUsuarioAsync(desativada.Id, usuario => usuario.Ativo = false);
        await fabrica.ConferirConexoesAsync();

        await conexaoDesativada.AguardarFechamentoAsync();
        var marca = Marca();
        await Hub.Clients.Group(GruposTempoReal.DoUsuario(ativa.Id)).LeadsAlterados([marca]);
        await conexaoAtiva.AguardarAsync(a => a.Ids.Contains(marca));
        Assert.False(conexaoAtiva.Fechada.IsCompleted);
    }

    [Fact]
    public async Task A_conferencia_encerra_a_conexao_de_quem_teve_a_senha_redefinida_pelo_admin()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        await using var conexao = await fabrica.ConectarAsync(atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var redefinicao = await admin.PostAsJsonAsync(
            $"/api/usuarios/{atendente.Id}/redefinir-senha", new { senhaProvisoria = "provisoria-do-teste" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, redefinicao.StatusCode);
        await fabrica.ConferirConexoesAsync();

        await conexao.AguardarFechamentoAsync();
    }

    [Fact]
    public async Task Quem_deixa_de_ser_admin_e_desconectado_e_volta_sem_o_grupo_dos_admins()
    {
        var usuario = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var cookie = await fabrica.CookieDaSessaoAsync(usuario);
        await using (var comoAdmin = await fabrica.ConectarComOCookieAsync(cookie))
        {
            using var outroAdmin = await fabrica.LogarComoAsync(Perfil.Admin);
            var edicao = await outroAdmin.PutAsJsonAsync(
                $"/api/usuarios/{usuario.Id}",
                new { nome = usuario.Nome, email = usuario.Email, perfil = "Atendente", ativo = true, participaRodizio = false },
                Ct);
            Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
            await fabrica.ConferirConexoesAsync();

            await comoAdmin.AguardarFechamentoAsync();
        }

        // A tela conecta de novo, com o mesmo cookie, que ainda diz Admin: o grupo vem do perfil gravado no banco.
        await using var comoAtendente = await fabrica.ConectarComOCookieAsync(cookie);
        var dosAdmins = Marca();
        var dela = Marca();
        await Hub.Clients.Group(GruposTempoReal.Admins).LeadsAlterados([dosAdmins]);
        await Hub.Clients.Group(GruposTempoReal.DoUsuario(usuario.Id)).LeadsAlterados([dela]);

        var antes = await comoAtendente.AvisosAteAsync(a => a.Ids.Contains(dela));
        Assert.DoesNotContain(antes, a => a.Ids.Contains(dosAdmins));
    }

    private async Task MudarUsuarioAsync(Guid id, Action<Usuario> mudar)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = (await usuarios.FindByIdAsync(id.ToString()))!;
        mudar(usuario);
        Assert.True((await usuarios.UpdateAsync(usuario)).Succeeded);
    }
}
