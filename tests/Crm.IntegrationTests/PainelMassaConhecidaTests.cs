using System.Net;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Critério de aceite do Passo 8: cada número do painel, conferido contra a massa de dados conhecida
/// (<see cref="MassaDoPainel"/>), num banco só dela. A massa é criada uma vez, e os testes só leem.
/// </summary>
public class PainelMassaConhecidaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<MassaDoPainel> MassaAsync() => MassaDoPainel.ObterAsync(fabrica);

    private static async Task<JsonElement> PainelAsync(HttpClient cliente, string query)
    {
        var resposta = await cliente.GetAsync($"/api/painel?{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static double? Numero(JsonElement valor) => valor.ValueKind == JsonValueKind.Null ? null : valor.GetDouble();

    private static (int LeadsNovos, int Entradas, int Ganhos, double? Taxa) Totais(JsonElement painel) => (
        painel.GetProperty("leadsNovos").GetInt32(),
        painel.GetProperty("entradas").GetInt32(),
        painel.GetProperty("ganhos").GetInt32(),
        Numero(painel.GetProperty("taxaConversao")));

    private static (double? Mediana, int ComContato, int SemContato) PrimeiroContato(JsonElement painel)
    {
        var contato = painel.GetProperty("primeiroContato");
        return (Numero(contato.GetProperty("medianaMinutos")), contato.GetProperty("comContato").GetInt32(), contato.GetProperty("semContato").GetInt32());
    }

    private static List<(string? Etapa, int LeadsNovos)> Situacao(JsonElement painel) =>
        [.. painel.GetProperty("situacaoAtual").EnumerateArray()
            .Select(s => (s.GetProperty("etapa").GetProperty("nome").GetString(), s.GetProperty("leadsNovos").GetInt32()))];

    private static List<(string? Origem, int LeadsNovos, int Ganhos, double? Taxa)> PorOrigem(JsonElement painel) =>
        [.. painel.GetProperty("porOrigem").EnumerateArray()
            .Select(o => (o.GetProperty("origem").GetString(), o.GetProperty("leadsNovos").GetInt32(), o.GetProperty("ganhos").GetInt32(), Numero(o.GetProperty("taxaConversao"))))];

    private static List<(string? Campanha, int LeadsNovos, int Ganhos, double? Taxa)> PorCampanha(JsonElement painel) =>
        [.. painel.GetProperty("porCampanha").EnumerateArray()
            .Select(c => (
                c.GetProperty("campanha").ValueKind == JsonValueKind.Null ? null : c.GetProperty("campanha").GetProperty("nome").GetString(),
                c.GetProperty("leadsNovos").GetInt32(),
                c.GetProperty("ganhos").GetInt32(),
                Numero(c.GetProperty("taxaConversao"))))];

    private static (int Leads, List<(string? Motivo, int Leads)> PorMotivo) Perdas(JsonElement painel)
    {
        var perdas = painel.GetProperty("perdas");
        return (
            perdas.GetProperty("leads").GetInt32(),
            [.. perdas.GetProperty("porMotivo").EnumerateArray()
                .Select(m => (m.GetProperty("motivo").GetProperty("nome").GetString(), m.GetProperty("leads").GetInt32()))]);
    }

    private static List<(Guid? Id, string? Nome, bool? Ativo, int LeadsNovos, int Ganhos, double? Taxa, double? Mediana)> PorAtendente(JsonElement painel) =>
        [.. painel.GetProperty("porAtendente").EnumerateArray()
            .Select(a =>
            {
                var responsavel = a.GetProperty("responsavel");
                var semResponsavel = responsavel.ValueKind == JsonValueKind.Null;
                return (
                    semResponsavel ? (Guid?)null : responsavel.GetProperty("id").GetGuid(),
                    semResponsavel ? null : responsavel.GetProperty("nome").GetString(),
                    semResponsavel ? (bool?)null : responsavel.GetProperty("ativo").GetBoolean(),
                    a.GetProperty("leadsNovos").GetInt32(),
                    a.GetProperty("ganhos").GetInt32(),
                    Numero(a.GetProperty("taxaConversao")),
                    Numero(a.GetProperty("medianaPrimeiroContatoMinutos")));
            })];

    [Fact]
    public async Task Admin_ve_cada_numero_da_massa()
    {
        var massa = await MassaAsync();
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        var painel = await PainelAsync(admin, MassaDoPainel.Setembro);

        Assert.Equal(("2026-09-01", "2026-09-30"), (painel.GetProperty("de").GetString(), painel.GetProperty("ate").GetString()));
        // A1 a S1: 8 leads novos, com as 8 entradas deles, a do site em A2 e a do Google em O1; ganharam A1 e P1.
        Assert.Equal((8, 10, 2, 0.25), Totais(painel));
        // Contato em 10, 20, 30, 60 e 90 minutos; A4, P2 e S1 ainda sem contato.
        Assert.Equal((30.0, 5, 3), PrimeiroContato(painel));
        Assert.Equal(
            [("Novo", 3), ("Em contato", 1), ("Qualificado", 0), ("Proposta enviada", 0), ("Ganho", 2), ("Perdido", 2)],
            Situacao(painel));
        Assert.Equal(
            [("meta_lead_ads", 4, 1, 0.25), ("google_ads", 2, 1, 0.5), ("site", 1, 0, 0.0), ("manual", 1, 0, 0.0)],
            PorOrigem(painel));
        Assert.Equal(
            [(massa.C1.Nome, 4, 1, 0.25), (massa.C2.Nome, 2, 1, 0.5), (null, 2, 0, 0.0)],
            PorCampanha(painel));

        // A3 e X1, e O1, que entrou em agosto e foi reaberto depois; T1, de teste, fica de fora.
        var perdas = Perdas(painel);
        Assert.Equal(3, perdas.Leads);
        Assert.Equal([("Preço", 1), ("Sem interesse", 1), ("Sem resposta", 1)], perdas.PorMotivo);

        List<(Guid?, string?, bool?, int, int, double?, double?)> porAtendente =
        [
            (massa.Adriana.Id, "Adriana", true, 5, 1, 0.2, 45.0),
            (massa.Pietra.Id, "Pietra", false, 2, 1, 0.5, 20.0),
            (null, null, null, 1, 0, 0.0, null),
        ];
        Assert.Equal(porAtendente, PorAtendente(painel));
    }

    [Fact]
    public async Task Filtros_de_origem_campanha_e_responsavel_conferem_com_a_massa()
    {
        var massa = await MassaAsync();
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        // As entradas valem pela origem e pela campanha de cada uma; os leads e as perdas, pelas da primeira entrada.
        var google = await PainelAsync(admin, $"{MassaDoPainel.Setembro}&origem=google_ads");
        Assert.Equal((2, 3, 1, 0.5), Totais(google));
        Assert.Equal([("Preço", 1)], Perdas(google).PorMotivo);

        var c1 = await PainelAsync(admin, $"{MassaDoPainel.Setembro}&campanhaId={massa.C1.Id}");
        Assert.Equal((4, 4, 1, 0.25), Totais(c1));
        Assert.Equal([("Sem interesse", 1), ("Sem resposta", 1)], Perdas(c1).PorMotivo);

        var pietra = await PainelAsync(admin, $"{MassaDoPainel.Setembro}&responsavelId={massa.Pietra.Id}");
        Assert.Equal((2, 2, 1, 0.5), Totais(pietra));
        Assert.Equal(0, Perdas(pietra).Leads);
        Assert.Equal([massa.Pietra.Id], PorAtendente(pietra).Select(a => a.Id));

        var semResponsavel = await PainelAsync(admin, $"{MassaDoPainel.Setembro}&semResponsavel=true");
        Assert.Equal((1, 1, 0, 0.0), Totais(semResponsavel));
        Assert.Equal([(Guid?)null], PorAtendente(semResponsavel).Select(a => a.Id));
    }

    [Fact]
    public async Task Periodo_conta_os_dias_no_horario_de_Brasilia()
    {
        var massa = await MassaAsync();
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        // O1 foi criado às 23:59:59 de 31/08 em Brasília, que já é 01/09 em UTC: fica fora.
        var primeiroDia = await PainelAsync(admin, "de=2026-09-01&ate=2026-09-01");
        // O2 entrou à 00:00 de 01/10, embora a origem o tenha recebido às 23:50 de 30/09: fica fora.
        var ultimoDia = await PainelAsync(admin, "de=2026-09-30&ate=2026-09-30");

        Assert.Equal((1, 1, 1, 1.0), Totais(primeiroDia));
        Assert.Equal([("meta_lead_ads", 1, 1, 1.0)], PorOrigem(primeiroDia));
        Assert.Equal((1, 1, 0, 0.0), Totais(ultimoDia));
        Assert.Equal([("manual", 1, 0, 0.0)], PorOrigem(ultimoDia));
    }

    [Fact]
    public async Task Sem_campanha_fica_sempre_por_ultimo_mesmo_com_mais_leads_que_as_campanhas()
    {
        var massa = await MassaAsync();
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        // De 15/09 a 30/09: P2 e A4 sem campanha, contra A3 em C2 e S1 em C1. No empate entre as campanhas, vale o nome.
        var painel = await PainelAsync(admin, "de=2026-09-15&ate=2026-09-30");

        Assert.Equal(
            [(massa.C2.Nome, 1, 0, 0.0), (massa.C1.Nome, 1, 0, 0.0), (null, 2, 0, 0.0)],
            PorCampanha(painel));
    }

    [Fact]
    public async Task Atendente_ve_so_os_proprios_numeros_mesmo_filtrando_por_outra_pessoa()
    {
        var massa = await MassaAsync();
        using var api = massa.CriarApi(fabrica);
        using var adriana = await api.LogarAsync(massa.Adriana);

        foreach (var filtro in new[] { "", $"&responsavelId={massa.Pietra.Id}", "&semResponsavel=true" })
        {
            var painel = await PainelAsync(adriana, MassaDoPainel.Setembro + filtro);

            // A1, A2, A3, A4 e X1, com as entradas deles e as que trouxeram A2 e O1 de volta.
            Assert.Equal((5, 7, 1, 0.2), Totais(painel));
            Assert.Equal((45.0, 4, 1), PrimeiroContato(painel));
            Assert.Equal(
                [("Novo", 1), ("Em contato", 1), ("Qualificado", 0), ("Proposta enviada", 0), ("Ganho", 1), ("Perdido", 2)],
                Situacao(painel));
            Assert.Equal(
                [("meta_lead_ads", 3, 1, 1.0 / 3), ("google_ads", 1, 0, 0.0), ("manual", 1, 0, 0.0)],
                PorOrigem(painel));
            Assert.Equal(
                [(massa.C1.Nome, 3, 1, 1.0 / 3), (massa.C2.Nome, 1, 0, 0.0), (null, 1, 0, 0.0)],
                PorCampanha(painel));
            Assert.Equal(3, Perdas(painel).Leads);
            Assert.Equal(JsonValueKind.Null, painel.GetProperty("porAtendente").ValueKind);
        }
    }
}
