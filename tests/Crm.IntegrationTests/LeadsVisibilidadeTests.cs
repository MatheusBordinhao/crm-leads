using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// A atendente só vê os próprios leads (regra que não pode quebrar). Lead de outra pessoa responde 404, nunca
/// 403, e não aparece na lista. Todo endpoint novo que toca leads ganha um teste aqui.
/// </summary>
public class LeadsVisibilidadeTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> EditarAsync(HttpClient cliente, long id) =>
        cliente.PutAsJsonAsync(
            $"/api/leads/{id}",
            new { nome = "Tentativa", email = LeadsDeTeste.EmailUnico(), telefone = (string?)null, cpfCnpj = (string?)null, uf = (string?)null, empresa = (string?)null, versao = 0u },
            Ct);

    private static async Task<List<long>> IdsDaListaAsync(HttpClient cliente, string query = "")
    {
        var lista = await (await cliente.GetAsync($"/api/leads?tamanho=100{query}", Ct)).LerJsonAsync();
        return [.. lista.GetProperty("itens").EnumerateArray().Select(l => l.GetProperty("id").GetInt64())];
    }

    [Fact]
    public async Task Atendente_recebe_404_no_lead_de_outra_atendente()
    {
        using var adriana = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var pietra = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadDaAdriana = await adriana.CriarLeadIdAsync();

        var ficha = await pietra.GetAsync($"/api/leads/{leadDaAdriana}", Ct);
        var edicao = await EditarAsync(pietra, leadDaAdriana);

        Assert.Equal(HttpStatusCode.NotFound, ficha.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edicao.StatusCode);
        Assert.Equal("Maria Souza", (await fabrica.BuscarLeadAsync(leadDaAdriana)).Nome);
    }

    [Fact]
    public async Task Atendente_recebe_404_no_lead_sem_responsavel_e_no_lead_de_teste()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var semResponsavel = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        var corpoTeste = LeadsDeTeste.CorpoIntegracao();
        corpoTeste["teste"] = true;
        var deTeste = await n8n.EnviarLeadIdAsync(corpoTeste);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        foreach (var id in new[] { semResponsavel, deTeste })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await atendente.GetAsync($"/api/leads/{id}", Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await EditarAsync(atendente, id)).StatusCode);
        }
    }

    [Fact]
    public async Task Atendente_recebe_404_ao_mudar_a_etapa_de_lead_que_nao_e_dela()
    {
        using var adriana = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var pietra = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpoTeste = LeadsDeTeste.CorpoIntegracao();
        corpoTeste["teste"] = true;
        var alheios = new[]
        {
            await adriana.CriarLeadIdAsync(),
            await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao()),
            await n8n.EnviarLeadIdAsync(corpoTeste),
        };
        var emContato = await fabrica.EtapaAsync("Em contato");

        foreach (var id in alheios)
        {
            Assert.Equal(HttpStatusCode.NotFound, (await pietra.MudarEtapaAsync(id, emContato.Id)).StatusCode);
            Assert.Equal("Novo", (await fabrica.BuscarLeadAsync(id)).Etapa.Nome);
            Assert.Empty(await fabrica.HistoricoAsync(id));
        }
    }

    [Fact]
    public async Task Lista_da_atendente_so_traz_os_leads_dela_mesmo_filtrando_por_outra_pessoa()
    {
        var pietraUsuario = await fabrica.CriarUsuarioAsync();
        using var adriana = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var pietra = await fabrica.LogarAsync(pietraUsuario);
        var daAdriana = new[] { await adriana.CriarLeadIdAsync(), await adriana.CriarLeadIdAsync() };
        await pietra.CriarLeadIdAsync();
        using var n8n = fabrica.CriarClienteIntegracao();
        await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());

        var lista = await IdsDaListaAsync(adriana);
        var filtrandoPelaPietra = await IdsDaListaAsync(adriana, $"&responsavelId={pietraUsuario.Id}");
        var semResponsavel = await IdsDaListaAsync(adriana, "&semResponsavel=true");

        Assert.Equal(daAdriana.Order(), lista.Order());
        Assert.Equal(daAdriana.Order(), filtrandoPelaPietra.Order());
        Assert.Equal(daAdriana.Order(), semResponsavel.Order());
    }

    [Fact]
    public async Task Admin_ve_e_edita_o_lead_de_qualquer_pessoa()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var id = await atendente.CriarLeadIdAsync();

        var ficha = await admin.GetAsync($"/api/leads/{id}", Ct);

        Assert.Equal(HttpStatusCode.OK, ficha.StatusCode);
        var versao = (await ficha.LerJsonAsync()).GetProperty("versao").GetUInt32();
        var edicao = await admin.PutAsJsonAsync(
            $"/api/leads/{id}",
            new { nome = "Nome do Admin", email = LeadsDeTeste.EmailUnico(), telefone = (string?)null, cpfCnpj = (string?)null, uf = (string?)null, empresa = (string?)null, versao },
            Ct);
        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        Assert.Contains(id, await IdsDaListaAsync(admin, $"&busca={Uri.EscapeDataString("Nome do Admin")}"));
    }
}
