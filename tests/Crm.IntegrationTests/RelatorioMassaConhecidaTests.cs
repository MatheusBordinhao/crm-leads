using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Critério de aceite do Passo 9: os números do relatório por campanha batem com os do painel. Cada linha do CSV é
/// conferida contra o painel filtrado por ela, e o total contra o painel sem filtros, na massa conhecida
/// (<see cref="MassaDoPainel"/>), que só é lida.
/// </summary>
public class RelatorioMassaConhecidaTests(CrmApiFactory fabrica)
{
    private const string SemCampanha = RelatorioCampanhasCsv.SemCampanha;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>As linhas do CSV, depois do BOM, com os campos separados. Os nomes da massa não têm ponto e vírgula.</summary>
    private static async Task<string[][]> CsvAsync(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/csv", resposta.Content.Headers.ContentType?.MediaType);
        var bytes = await resposta.Content.ReadAsByteArrayAsync(Ct);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var texto = Encoding.UTF8.GetString(bytes[3..]);
        Assert.EndsWith("\r\n", texto, StringComparison.Ordinal);
        return [.. texto[..^2].Split("\r\n").Select(l => l.Split(';'))];
    }

    private static async Task<JsonElement> JsonAsync(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static double? Numero(JsonElement valor) => valor.ValueKind == JsonValueKind.Null ? null : valor.GetDouble();

    private static string Inteiro(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Os números do painel na ordem e no formato das colunas do CSV: leads novos, entradas, em andamento (as etapas
    /// abertas da situação atual), ganhos, perdidos (a etapa Perdido), taxa de conversão e mediana do primeiro contato.
    /// </summary>
    private static string[] NumerosDoPainel(JsonElement painel)
    {
        var situacao = painel.GetProperty("situacaoAtual").EnumerateArray()
            .Select(s => (Tipo: s.GetProperty("etapa").GetProperty("tipo").GetString(), Leads: s.GetProperty("leadsNovos").GetInt32()))
            .ToList();
        return
        [
            Inteiro(painel.GetProperty("leadsNovos").GetInt32()),
            Inteiro(painel.GetProperty("entradas").GetInt32()),
            Inteiro(situacao.Where(s => s.Tipo == "aberta").Sum(s => s.Leads)),
            Inteiro(painel.GetProperty("ganhos").GetInt32()),
            Inteiro(situacao.Where(s => s.Tipo == "perdido").Sum(s => s.Leads)),
            RelatorioCampanhasCsv.Porcentagem(Numero(painel.GetProperty("taxaConversao"))),
            RelatorioCampanhasCsv.Minutos(Numero(painel.GetProperty("primeiroContato").GetProperty("medianaMinutos"))),
        ];
    }

    [Fact]
    public async Task Cada_linha_do_CSV_bate_com_o_painel_filtrado_por_ela_e_o_total_com_o_painel_inteiro()
    {
        var massa = await MassaDoPainel.ObterAsync(fabrica);
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        var csv = await CsvAsync(admin, $"/api/relatorios/campanhas/csv?{MassaDoPainel.Setembro}");

        // C1: A1 (ganho), A2 e S1 (em andamento) e X1 (perdido, anonimizado, que continua contando); O1 entrou em agosto
        // e T1 é de teste. C2: A3 (perdido) e P1 (ganho), com a entrada que trouxe O1 de volta. No site, P2 e a nova
        // entrada de A2; no cadastro manual, A4.
        Assert.Equal(RelatorioCampanhasCsv.Cabecalho, csv[0]);
        string[][] linhas =
        [
            ["Meta", massa.C1.Nome, "4", "4", "2", "1", "1", "25%", "30"],
            ["Google Ads", massa.C2.Nome, "2", "3", "0", "1", "1", "50%", "40"],
            ["Site", SemCampanha, "1", "2", "1", "0", "0", "0%", ""],
            ["Cadastro manual", SemCampanha, "1", "1", "1", "0", "0", "0%", ""],
            ["Total", "", "8", "10", "4", "2", "2", "25%", "30"],
        ];
        Assert.Equal(linhas, csv[1..]);

        // Cada linha com o painel filtrado por ela: a campanha ou, nas linhas sem campanha, a origem, porque na massa o
        // site e o cadastro manual só têm leads sem campanha. O total, com o painel sem filtros.
        string[] filtros = [$"&campanhaId={massa.C1.Id}", $"&campanhaId={massa.C2.Id}", "&origem=site", "&origem=manual", ""];
        foreach (var (linha, filtro) in csv[1..].Zip(filtros))
        {
            var painel = await JsonAsync(admin, $"/api/painel?{MassaDoPainel.Setembro}{filtro}");
            Assert.Equal(NumerosDoPainel(painel), linha[2..]);
        }
    }

    [Fact]
    public async Task As_linhas_de_cada_origem_somam_o_por_origem_do_painel_e_as_sem_campanha_a_linha_sem_campanha()
    {
        var massa = await MassaDoPainel.ObterAsync(fabrica);
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        var relatorio = await JsonAsync(admin, $"/api/relatorios/campanhas?{MassaDoPainel.Setembro}");
        var painel = await JsonAsync(admin, $"/api/painel?{MassaDoPainel.Setembro}");

        var linhas = relatorio.GetProperty("linhas").EnumerateArray().ToList();
        var porOrigem = linhas
            .GroupBy(l => l.GetProperty("origem").GetString())
            .Select(g => (g.Key, g.Sum(l => l.GetProperty("leadsNovos").GetInt32()), g.Sum(l => l.GetProperty("ganhos").GetInt32())))
            .OrderBy(o => o.Key, StringComparer.Ordinal);
        var porOrigemDoPainel = painel.GetProperty("porOrigem").EnumerateArray()
            .Select(o => (o.GetProperty("origem").GetString(), o.GetProperty("leadsNovos").GetInt32(), o.GetProperty("ganhos").GetInt32()))
            .OrderBy(o => o.Item1, StringComparer.Ordinal);
        Assert.Equal(porOrigemDoPainel, porOrigem);

        var semCampanha = linhas.Where(l => l.GetProperty("campanha").ValueKind == JsonValueKind.Null).ToList();
        var semCampanhaDoPainel = painel.GetProperty("porCampanha").EnumerateArray()
            .Single(c => c.GetProperty("campanha").ValueKind == JsonValueKind.Null);
        Assert.Equal(
            (semCampanhaDoPainel.GetProperty("leadsNovos").GetInt32(), semCampanhaDoPainel.GetProperty("ganhos").GetInt32()),
            (semCampanha.Sum(l => l.GetProperty("leadsNovos").GetInt32()), semCampanha.Sum(l => l.GetProperty("ganhos").GetInt32())));
    }

    [Fact]
    public async Task O_JSON_traz_a_campanha_a_origem_e_os_numeros_com_a_taxa_de_0_a_1()
    {
        var massa = await MassaDoPainel.ObterAsync(fabrica);
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);

        var relatorio = await JsonAsync(admin, $"/api/relatorios/campanhas?{MassaDoPainel.Setembro}");

        Assert.Equal(("2026-09-01", "2026-09-30"), (relatorio.GetProperty("de").GetString(), relatorio.GetProperty("ate").GetString()));
        var linhas = relatorio.GetProperty("linhas").EnumerateArray()
            .Select(l => (
                l.GetProperty("origem").GetString(),
                l.GetProperty("campanha").ValueKind == JsonValueKind.Null ? (int?)null : l.GetProperty("campanha").GetProperty("id").GetInt32(),
                l.GetProperty("leadsNovos").GetInt32(),
                l.GetProperty("entradas").GetInt32(),
                l.GetProperty("emAndamento").GetInt32(),
                l.GetProperty("ganhos").GetInt32(),
                l.GetProperty("perdidos").GetInt32(),
                Numero(l.GetProperty("taxaConversao")),
                Numero(l.GetProperty("medianaPrimeiroContatoMinutos"))))
            .ToList();
        List<(string?, int?, int, int, int, int, int, double?, double?)> esperadas =
        [
            ("meta_lead_ads", massa.C1.Id, 4, 4, 2, 1, 1, 0.25, 30.0),
            ("google_ads", massa.C2.Id, 2, 3, 0, 1, 1, 0.5, 40.0),
            ("site", null, 1, 2, 1, 0, 0, 0.0, null),
            ("manual", null, 1, 1, 1, 0, 0, 0.0, null),
        ];
        Assert.Equal(esperadas, linhas);
        Assert.Equal(massa.C1.Nome, relatorio.GetProperty("linhas")[0].GetProperty("campanha").GetProperty("nome").GetString());

        var total = relatorio.GetProperty("total");
        Assert.Equal(
            (8, 10, 4, 2, 2, (double?)0.25, (double?)30.0),
            (total.GetProperty("leadsNovos").GetInt32(), total.GetProperty("entradas").GetInt32(), total.GetProperty("emAndamento").GetInt32(),
                total.GetProperty("ganhos").GetInt32(), total.GetProperty("perdidos").GetInt32(), Numero(total.GetProperty("taxaConversao")),
                Numero(total.GetProperty("medianaPrimeiroContatoMinutos"))));
    }

    [Fact]
    public async Task A_integracao_recebe_o_mesmo_JSON_e_o_mesmo_CSV_da_tela()
    {
        var massa = await MassaDoPainel.ObterAsync(fabrica);
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        using var n8n = api.CriarClienteIntegracao();

        foreach (var formato in new[] { "", "/csv" })
        {
            var daTela = await admin.GetAsync($"/api/relatorios/campanhas{formato}?{MassaDoPainel.Setembro}", Ct);
            var daIntegracao = await n8n.GetAsync($"/api/integracoes/relatorios/campanhas{formato}?{MassaDoPainel.Setembro}", Ct);

            Assert.Equal(HttpStatusCode.OK, daTela.StatusCode);
            Assert.Equal(HttpStatusCode.OK, daIntegracao.StatusCode);
            Assert.Equal(daTela.Content.Headers.ContentType?.MediaType, daIntegracao.Content.Headers.ContentType?.MediaType);
            Assert.Equal(await daTela.Content.ReadAsByteArrayAsync(Ct), await daIntegracao.Content.ReadAsByteArrayAsync(Ct));
        }

        var csv = await n8n.GetAsync($"/api/integracoes/relatorios/campanhas/csv?{MassaDoPainel.Setembro}", Ct);
        Assert.Equal("relatorio-campanhas-2026-09-01-a-2026-09-30.csv", csv.Content.Headers.ContentDisposition?.FileName);
    }

    [Fact]
    public async Task Um_dia_so_com_uma_entrada_de_quem_ja_era_lead_da_a_linha_da_campanha_sem_leads_novos()
    {
        var massa = await MassaDoPainel.ObterAsync(fabrica);
        using var api = massa.CriarApi(fabrica);
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        const string Dia = "de=2026-09-18&ate=2026-09-18";

        // Em 18/09, nenhum lead foi criado: só a entrada do Google que trouxe O1 de volta, na campanha C2.
        var csv = await CsvAsync(admin, $"/api/relatorios/campanhas/csv?{Dia}");
        var painel = await JsonAsync(admin, $"/api/painel?{Dia}");

        string[][] linhas =
        [
            ["Google Ads", massa.C2.Nome, "0", "1", "0", "0", "0", "", ""],
            ["Total", "", "0", "1", "0", "0", "0", "", ""],
        ];
        Assert.Equal(linhas, csv[1..]);
        Assert.Equal(NumerosDoPainel(painel), csv[^1][2..]);
    }
}
