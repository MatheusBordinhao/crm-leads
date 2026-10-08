using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Painel;

namespace Crm.UnitTests.Painel;

public class MontagemRelatorioTests
{
    private static readonly DateTimeOffset CriadoEm = new(2026, 9, 10, 15, 0, 0, TimeSpan.Zero);

    private static readonly PeriodoPainel Setembro = new(new(2026, 9, 1), new(2026, 9, 30));

    private static readonly CampanhaDoRelatorio Captacao = new(1, Origem.MetaLeadAds, "Meta | Captação");

    private static readonly CampanhaDoRelatorio Marca = new(2, Origem.GoogleAds, "Google | Marca");

    private static readonly CampanhaDoRelatorio Remarketing = new(3, Origem.MetaLeadAds, "Meta | Remarketing");

    private static LeadDaCoorte Lead(Origem origem, CampanhaDoRelatorio? campanha, TipoEtapa tipo, int? minutosAteOContato = null) => new(
        EtapaId: (int)tipo,
        TipoEtapa: tipo,
        ResponsavelId: null,
        Origem: origem,
        CampanhaId: campanha?.Id,
        CriadoEm: CriadoEm,
        PrimeiroContatoEm: minutosAteOContato is { } minutos ? CriadoEm.AddMinutes(minutos) : null);

    private static LeadDaCoorte Lead(CampanhaDoRelatorio campanha, TipoEtapa tipo, int? minutosAteOContato = null) =>
        Lead(campanha.Origem, campanha, tipo, minutosAteOContato);

    private static RelatorioCampanhasResponse Montar(
        IReadOnlyCollection<LeadDaCoorte> coorte, IReadOnlyCollection<EntradasDoGrupo>? entradas = null) =>
        MontagemRelatorio.Montar(Setembro, coorte, entradas ?? [], [Captacao, Marca, Remarketing]);

    private static string Rotulo(LinhaRelatorioResponse linha) => linha.Campanha?.Nome ?? $"sem campanha ({linha.Origem})";

    [Fact]
    public void Agrupa_pela_campanha_e_os_sem_campanha_pela_origem_sempre_no_fim()
    {
        var relatorio = Montar(
        [
            Lead(Origem.Site, null, TipoEtapa.Aberta),
            Lead(Origem.Site, null, TipoEtapa.Aberta),
            Lead(Origem.Site, null, TipoEtapa.Ganho),
            Lead(Origem.Manual, null, TipoEtapa.Aberta),
            Lead(Captacao, TipoEtapa.Ganho, 10),
            Lead(Captacao, TipoEtapa.Aberta, 30),
            Lead(Marca, TipoEtapa.Perdido, 60),
        ]);

        // Os sem campanha são a maioria, mas não vieram de anúncio: ficam no fim, como no painel.
        Assert.Equal(
            ["Meta | Captação", "Google | Marca", "sem campanha (Site)", "sem campanha (Manual)"],
            relatorio.Linhas.Select(Rotulo));
        Assert.Equal(
            [Origem.MetaLeadAds, Origem.GoogleAds, Origem.Site, Origem.Manual],
            relatorio.Linhas.Select(l => l.Origem));
        Assert.Equal(
            new LinhaRelatorioResponse(Origem.MetaLeadAds, new CampanhaResumo(1, "Meta | Captação"), 2, 0, 1, 1, 0, 0.5, 20),
            relatorio.Linhas[0]);
        Assert.Equal(new LinhaRelatorioResponse(Origem.Site, null, 3, 0, 2, 1, 0, 1.0 / 3, null), relatorio.Linhas[2]);
    }

    [Fact]
    public void Em_andamento_ganhos_e_perdidos_somam_os_leads_novos_de_cada_linha()
    {
        var relatorio = Montar(
        [
            Lead(Captacao, TipoEtapa.Aberta),
            Lead(Captacao, TipoEtapa.Aberta),
            Lead(Captacao, TipoEtapa.Ganho),
            Lead(Captacao, TipoEtapa.Perdido),
            Lead(Captacao, TipoEtapa.Perdido),
            Lead(Origem.Indicacao, null, TipoEtapa.Perdido),
        ]);

        Assert.All(relatorio.Linhas, l => Assert.Equal(l.LeadsNovos, l.EmAndamento + l.Ganhos + l.Perdidos));
        Assert.Equal((5, 2, 1, 2), (relatorio.Linhas[0].LeadsNovos, relatorio.Linhas[0].EmAndamento, relatorio.Linhas[0].Ganhos, relatorio.Linhas[0].Perdidos));
        Assert.Equal((6, 2, 1, 3), (relatorio.Total.LeadsNovos, relatorio.Total.EmAndamento, relatorio.Total.Ganhos, relatorio.Total.Perdidos));
    }

    [Fact]
    public void As_entradas_contam_pela_campanha_ou_pela_origem_de_cada_uma()
    {
        var relatorio = Montar(
            [Lead(Captacao, TipoEtapa.Aberta), Lead(Origem.Site, null, TipoEtapa.Aberta)],
            [
                new(Origem.MetaLeadAds, Captacao.Id, 3),
                new(Origem.Site, null, 2),
                new(Origem.Manual, null, 1),
            ]);

        Assert.Equal(
            [("Meta | Captação", 1, 3), ("sem campanha (Site)", 1, 2), ("sem campanha (Manual)", 0, 1)],
            relatorio.Linhas.Select(l => (Rotulo(l), l.LeadsNovos, l.Entradas)));
        Assert.Equal(6, relatorio.Total.Entradas);
    }

    [Fact]
    public void Campanha_so_com_entradas_no_periodo_aparece_sem_taxa_nem_mediana()
    {
        // Uma pessoa que já era lead voltou pela campanha do Google: entrada sem lead novo.
        var relatorio = Montar([], [new(Origem.GoogleAds, Marca.Id, 1)]);

        var linha = Assert.Single(relatorio.Linhas);
        Assert.Equal(new LinhaRelatorioResponse(Origem.GoogleAds, new CampanhaResumo(2, "Google | Marca"), 0, 1, 0, 0, 0, null, null), linha);
        Assert.Equal(new TotalRelatorioResponse(0, 1, 0, 0, 0, null, null), relatorio.Total);
    }

    [Fact]
    public void No_empate_vale_o_nome_da_campanha_e_nos_sem_campanha_a_ordem_das_origens()
    {
        var relatorio = Montar(
        [
            Lead(Origem.Manual, null, TipoEtapa.Aberta),
            Lead(Remarketing, TipoEtapa.Aberta),
            Lead(Origem.Whatsapp, null, TipoEtapa.Aberta),
            Lead(Marca, TipoEtapa.Aberta),
            Lead(Captacao, TipoEtapa.Aberta),
            Lead(Captacao, TipoEtapa.Aberta),
        ]);

        Assert.Equal(
            ["Meta | Captação", "Google | Marca", "Meta | Remarketing", "sem campanha (Whatsapp)", "sem campanha (Manual)"],
            relatorio.Linhas.Select(Rotulo));
    }

    [Fact]
    public void Total_e_o_resumo_de_todos_os_leads_novos_com_todas_as_entradas()
    {
        LeadDaCoorte[] coorte =
        [
            Lead(Captacao, TipoEtapa.Ganho, 10),
            Lead(Captacao, TipoEtapa.Aberta, 30),
            Lead(Marca, TipoEtapa.Perdido, 90),
            Lead(Origem.Site, null, TipoEtapa.Aberta),
        ];

        var relatorio = Montar(coorte, [new(Origem.MetaLeadAds, Captacao.Id, 2), new(Origem.GoogleAds, Marca.Id, 2), new(Origem.Site, null, 1)]);

        var resumo = MetricasPainel.Resumir(coorte);
        Assert.Equal(
            new TotalRelatorioResponse(4, 5, 2, 1, 1, resumo.TaxaConversao, resumo.MedianaPrimeiroContatoMinutos),
            relatorio.Total);
        Assert.Equal((0.25, 30.0), (relatorio.Total.TaxaConversao, relatorio.Total.MedianaPrimeiroContatoMinutos));
        Assert.Equal((Setembro.De, Setembro.Ate), (relatorio.De, relatorio.Ate));
    }

    [Fact]
    public void Sem_nada_no_periodo_nao_ha_linhas_e_o_total_fica_zerado()
    {
        var relatorio = Montar([]);

        Assert.Empty(relatorio.Linhas);
        Assert.Equal(new TotalRelatorioResponse(0, 0, 0, 0, 0, null, null), relatorio.Total);
    }
}
