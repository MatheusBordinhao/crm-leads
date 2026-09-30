using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Cadastro de etapas pelo admin. Estes testes só mexem nas etapas que eles mesmos criam, que ficam no fim das
/// abertas: a etapa inicial continua sendo Novo para os outros testes.
/// </summary>
public class EtapasConfigTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int IdDe(JsonElement etapa) => etapa.GetProperty("id").GetInt32();

    private static string Texto(int id) => id.ToString(CultureInfo.InvariantCulture);

    private static Task<HttpResponseMessage> DesativarAsync(HttpClient admin, int id, int? moverLeadsParaId = null) =>
        admin.PostAsJsonAsync($"/api/etapas/{id}/desativar", new { moverLeadsParaId }, Ct);

    private async Task<List<JsonElement>> EtapasAsync(HttpClient cliente) =>
        [.. (await (await cliente.GetAsync("/api/etapas", Ct)).LerJsonAsync()).EnumerateArray()];

    /// <summary>Coloca o lead na etapa direto no banco, sem passar pela etapa inicial.</summary>
    private async Task ColocarNaEtapaAsync(long leadId, int etapaId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .Where(l => l.Id == leadId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.EtapaId, etapaId), Ct);
    }

    [Fact]
    public async Task Admin_cria_etapa_aberta_no_fim_das_abertas_antes_de_Ganho_e_Perdido()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var nome = $"Negociação {Guid.NewGuid():N}"[..30];

        var resposta = await admin.PostAsJsonAsync("/api/etapas", new { nome = $"  {nome}  " }, Ct);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criada = await resposta.LerJsonAsync();
        Assert.Equal((nome, "aberta", true), (criada.GetProperty("nome").GetString(), criada.GetProperty("tipo").GetString(), criada.GetProperty("ativa").GetBoolean()));
        var etapas = await EtapasAsync(admin);
        int Ordem(Func<JsonElement, bool> filtro) => etapas.Single(filtro).GetProperty("ordem").GetInt32();
        var ordem = Ordem(e => IdDe(e) == IdDe(criada));
        Assert.True(ordem > Ordem(e => e.GetProperty("nome").GetString() == "Proposta enviada"));
        Assert.True(ordem < Ordem(e => e.GetProperty("tipo").GetString() == "ganho" && e.GetProperty("ativa").GetBoolean()));
        Assert.True(ordem < Ordem(e => e.GetProperty("tipo").GetString() == "perdido" && e.GetProperty("ativa").GetBoolean()));
        var registro = Assert.Single(await fabrica.AuditoriaAsync(EntidadesAuditoria.Etapa, Texto(IdDe(criada))));
        Assert.Equal(AcoesAuditoria.EtapaCriada, registro.Acao);
    }

    [Fact]
    public async Task Nome_repetido_e_recusado_sem_diferenciar_maiusculas()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var outra = await admin.CriarEtapaAsync();

        var criarNovo = await admin.PostAsJsonAsync("/api/etapas", new { nome = "NOVO" }, Ct);
        var renomear = await admin.PutAsJsonAsync($"/api/etapas/{IdDe(outra)}", new { nome = "em contato" }, Ct);
        var vazio = await admin.PostAsJsonAsync("/api/etapas", new { nome = " " }, Ct);

        Assert.Equal([RegrasEtapas.MensagemNomeRepetido], await criarNovo.LerErrosDoCampoAsync("nome"));
        Assert.Equal([RegrasEtapas.MensagemNomeRepetido], await renomear.LerErrosDoCampoAsync("nome"));
        Assert.Equal(["Informe o nome."], await vazio.LerErrosDoCampoAsync("nome"));
    }

    [Fact]
    public async Task Admin_renomeia_a_etapa_e_a_auditoria_guarda_o_campo()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var etapa = await admin.CriarEtapaAsync();
        var nome = $"Renomeada {Guid.NewGuid():N}"[..30];

        var resposta = await admin.PutAsJsonAsync($"/api/etapas/{IdDe(etapa)}", new { nome }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(nome, (await resposta.LerJsonAsync()).GetProperty("nome").GetString());
        var edicao = (await fabrica.AuditoriaAsync(EntidadesAuditoria.Etapa, Texto(IdDe(etapa)))).Last();
        Assert.Equal(AcoesAuditoria.EtapaEditada, edicao.Acao);
        Assert.Equal("nome", Assert.Single(edicao.Detalhes!.RootElement.GetProperty("campos").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task Etapa_com_leads_so_e_desativada_movendo_os_leads_com_historico()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var etapa = await admin.CriarEtapaAsync();
        var leads = new[] { await admin.CriarLeadIdAsync(), await admin.CriarLeadIdAsync() };
        foreach (var lead in leads)
        {
            await ColocarNaEtapaAsync(lead, IdDe(etapa));
        }

        var emContato = await fabrica.EtapaAsync("Em contato");

        var semDestino = await DesativarAsync(admin, IdDe(etapa));
        var comDestino = await DesativarAsync(admin, IdDe(etapa), emContato.Id);

        Assert.Equal(HttpStatusCode.Conflict, semDestino.StatusCode);
        var problema = await semDestino.LerProblemaAsync();
        Assert.Equal(RegrasEtapas.MensagemTemLeads(2), problema.GetProperty("detail").GetString());
        Assert.Equal(2, problema.GetProperty("quantidadeLeads").GetInt32());
        Assert.Equal(HttpStatusCode.OK, comDestino.StatusCode);
        Assert.False((await comDestino.LerJsonAsync()).GetProperty("ativa").GetBoolean());
        foreach (var lead in leads)
        {
            var movido = await fabrica.BuscarLeadAsync(lead);
            Assert.Equal(emContato.Id, movido.EtapaId);
            Assert.Null(movido.PrimeiroContatoEm);
            var mudanca = Assert.Single(await fabrica.HistoricoAsync(lead));
            Assert.Equal((IdDe(etapa), emContato.Id, eu.Id), (mudanca.EtapaAnteriorId, mudanca.EtapaNovaId, mudanca.UsuarioId));
        }

        var desativacao = (await fabrica.AuditoriaAsync(EntidadesAuditoria.Etapa, Texto(IdDe(etapa)))).Last();
        Assert.Equal(AcoesAuditoria.EtapaDesativada, desativacao.Acao);
        Assert.Equal(2, desativacao.Detalhes!.RootElement.GetProperty("leadsMovidos").GetInt32());
    }

    [Fact]
    public async Task Destino_dos_leads_precisa_ser_outra_etapa_aberta_e_ativa()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var etapa = await admin.CriarEtapaAsync();
        var desativada = await admin.CriarEtapaAsync();
        Assert.Equal(HttpStatusCode.OK, (await DesativarAsync(admin, IdDe(desativada))).StatusCode);
        var lead = await admin.CriarLeadIdAsync();
        await ColocarNaEtapaAsync(lead, IdDe(etapa));

        // Nem Ganho nem Perdido: mover em lote para Perdido pularia o motivo, e para Ganho, o documento.
        var ganho = (await fabrica.EtapaAsync("Ganho")).Id;
        var perdido = (await fabrica.EtapaAsync("Perdido")).Id;
        foreach (var destino in new[] { IdDe(etapa), IdDe(desativada), ganho, perdido, int.MaxValue })
        {
            var resposta = await DesativarAsync(admin, IdDe(etapa), destino);

            Assert.Equal([RegrasEtapas.MensagemDestinoInvalido], await resposta.LerErrosDoCampoAsync("moverLeadsParaId"));
        }

        Assert.Equal(IdDe(etapa), (await fabrica.BuscarLeadAsync(lead)).EtapaId);
        Assert.Contains(await EtapasAsync(admin), e => IdDe(e) == IdDe(etapa) && e.GetProperty("ativa").GetBoolean());
    }

    [Fact]
    public async Task Ganho_e_Perdido_nao_sao_desativadas()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        foreach (var nome in new[] { "Ganho", "Perdido" })
        {
            var resposta = await DesativarAsync(admin, (await fabrica.EtapaAsync(nome)).Id, (await fabrica.EtapaAsync("Novo")).Id);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal(RegrasEtapas.MensagemFechamentoFixo, await resposta.LerDetalheAsync());
            Assert.True((await fabrica.EtapaAsync(nome)).Ativa);
        }
    }

    [Fact]
    public async Task Etapa_desativada_sai_do_funil_e_pode_ser_reativada()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var etapa = await admin.CriarEtapaAsync();
        var lead = await atendente.CriarLeadIdAsync();

        await DesativarAsync(admin, IdDe(etapa));
        var mudancaNaDesativada = await atendente.MudarEtapaAsync(lead, IdDe(etapa));
        var reativacao = await admin.PostAsync($"/api/etapas/{IdDe(etapa)}/reativar", null, Ct);
        var mudancaNaReativada = await atendente.MudarEtapaAsync(lead, IdDe(etapa));

        Assert.Equal(HttpStatusCode.BadRequest, mudancaNaDesativada.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reativacao.StatusCode);
        Assert.True((await reativacao.LerJsonAsync()).GetProperty("ativa").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, mudancaNaReativada.StatusCode);
        Assert.Equal(
            [AcoesAuditoria.EtapaCriada, AcoesAuditoria.EtapaDesativada, AcoesAuditoria.EtapaReativada],
            (await fabrica.AuditoriaAsync(EntidadesAuditoria.Etapa, Texto(IdDe(etapa)))).Select(r => r.Acao));
    }

    [Fact]
    public async Task Etapa_inexistente_responde_404()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/etapas/{int.MaxValue}", new { nome = "X" }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await DesativarAsync(admin, int.MaxValue)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/etapas/{int.MaxValue}/reativar", null, Ct)).StatusCode);
    }
}
