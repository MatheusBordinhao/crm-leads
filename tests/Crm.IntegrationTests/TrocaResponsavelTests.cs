using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>Troca de responsável pelo admin (seção 3.3). O 403 da atendente está em LeadsVisibilidadeTests.</summary>
public class TrocaResponsavelTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> TrocarAsync(HttpClient admin, long leadId, Guid responsavelId) =>
        admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { responsavelId }, Ct);

    private async Task<List<RegistroAuditoria>> TrocasAsync(long leadId) =>
        [.. (await fabrica.AuditoriaAsync(EntidadesAuditoria.Lead, leadId.ToString(CultureInfo.InvariantCulture)))
            .Where(r => r.Acao == AcoesAuditoria.LeadReatribuido)];

    [Fact]
    public async Task Admin_passa_o_lead_para_outra_pessoa_com_auditoria_linha_do_tempo_e_aviso()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var pietraUsuario = await fabrica.CriarUsuarioAsync();
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        using var admin = await fabrica.LogarAsync(eu);
        var leadId = await adriana.CriarLeadIdAsync();

        var resposta = await TrocarAsync(admin, leadId, pietraUsuario.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(pietraUsuario.Id, (await resposta.LerJsonAsync()).GetProperty("responsavel").GetProperty("id").GetGuid());
        var troca = Assert.Single(await TrocasAsync(leadId));
        Assert.Equal(eu.Id, troca.UsuarioId);
        Assert.Equal(adrianaUsuario.Id, troca.Detalhes!.RootElement.GetProperty("anteriorId").GetGuid());
        Assert.Equal(pietraUsuario.Id, troca.Detalhes.RootElement.GetProperty("novoId").GetGuid());

        var item = (await admin.LinhaDoTempoAsync(leadId))[0];
        Assert.Equal("responsavel", item.GetProperty("tipo").GetString());
        Assert.Equal(adrianaUsuario.Id, item.GetProperty("responsavel").GetProperty("anterior").GetProperty("id").GetGuid());
        Assert.Equal(pietraUsuario.Id, item.GetProperty("responsavel").GetProperty("nova").GetProperty("id").GetGuid());

        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(pietraUsuario.Id, leadId));
        Assert.Equal(TipoNotificacao.LeadReatribuido, aviso.Tipo);
        Assert.Equal($"{eu.Nome} passou um lead para você", aviso.Titulo);
        Assert.Equal(HttpStatusCode.NotFound, (await adriana.GetAsync($"/api/leads/{leadId}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Lead_sem_responsavel_ganha_uma()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var dona = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await dona.CriarLeadIdAsync();
        await fabrica.PassarParaAsync(leadId, null);

        var resposta = await TrocarAsync(admin, leadId, atendente.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var troca = Assert.Single(await TrocasAsync(leadId));
        Assert.Equal(JsonValueKind.Null, troca.Detalhes!.RootElement.GetProperty("anteriorId").ValueKind);
        Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(atendente.Id, leadId));
    }

    [Fact]
    public async Task Passar_para_a_mesma_pessoa_nao_muda_nada()
    {
        var atendenteUsuario = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(atendenteUsuario);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var leadId = await atendente.CriarLeadIdAsync();
        var antes = await fabrica.BuscarLeadAsync(leadId);

        var resposta = await TrocarAsync(admin, leadId, atendenteUsuario.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Empty(await TrocasAsync(leadId));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(atendenteUsuario.Id, leadId));
        Assert.Equal(antes.AtualizadoEm, (await fabrica.BuscarLeadAsync(leadId)).AtualizadoEm);
    }

    [Fact]
    public async Task Admin_que_passa_o_lead_para_si_nao_recebe_aviso()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        var resposta = await TrocarAsync(admin, leadId, eu.Id);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(eu.Id, (await fabrica.BuscarLeadAsync(leadId)).ResponsavelId);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(eu.Id, leadId));
    }

    [Fact]
    public async Task A_troca_nao_mexe_na_vez_do_rodizio()
    {
        var nova = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        await TrocarAsync(admin, leadId, nova.Id);

        Assert.Null((await fabrica.BuscarUsuarioAsync(nova.Id)).UltimoLeadAtribuidoEm);
    }

    [Fact]
    public async Task Responsavel_inexistente_ou_desativada_responde_400()
    {
        var desativada = await fabrica.CriarUsuarioAsync(ativo: false);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var dona = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(dona);
        var leadId = await atendente.CriarLeadIdAsync();

        foreach (var responsavelId in new[] { desativada.Id, Guid.NewGuid() })
        {
            var resposta = await TrocarAsync(admin, leadId, responsavelId);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal([LeadsController.MensagemResponsavelInvalido], await resposta.LerErrosDoCampoAsync("responsavelId"));
        }

        Assert.Equal(dona.Id, (await fabrica.BuscarLeadAsync(leadId)).ResponsavelId);
    }

    [Fact]
    public async Task Responsavel_ausente_responde_400()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        var resposta = await admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Lead_inexistente_responde_404()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var resposta = await TrocarAsync(admin, long.MaxValue, atendente.Id);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Lead_de_teste_continua_sem_responsavel()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;
        var leadId = await n8n.EnviarLeadIdAsync(corpo);

        var resposta = await TrocarAsync(admin, leadId, atendente.Id);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(LeadsController.MensagemLeadDeTeste, await resposta.LerDetalheAsync());
        Assert.Null((await fabrica.BuscarLeadAsync(leadId)).ResponsavelId);
    }
}
