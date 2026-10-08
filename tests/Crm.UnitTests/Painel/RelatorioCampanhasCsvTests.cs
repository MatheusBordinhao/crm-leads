using System.Text;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Painel;

namespace Crm.UnitTests.Painel;

public class RelatorioCampanhasCsvTests
{
    private static readonly DateOnly De = new(2026, 9, 1);

    private static readonly DateOnly Ate = new(2026, 9, 30);

    private static string Texto(RelatorioCampanhasResponse relatorio)
    {
        var bytes = RelatorioCampanhasCsv.Gerar(relatorio);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    [Fact]
    public void Gera_o_cabecalho_uma_linha_por_campanha_e_o_total_com_os_numeros_do_Excel()
    {
        var relatorio = new RelatorioCampanhasResponse(
            De,
            Ate,
            [
                new(Origem.MetaLeadAds, new CampanhaResumo(1, "Outubro | Serviço Y"), 3, 4, 1, 1, 1, 1.0 / 3, 12.5),
                new(Origem.GoogleAds, new CampanhaResumo(2, "Google · campanha 123"), 0, 1, 0, 0, 0, null, null),
                new(Origem.Site, null, 1, 2, 1, 0, 0, 0, null),
            ],
            new TotalRelatorioResponse(4, 7, 2, 1, 1, 0.25, 12.5));

        Assert.Equal(
            "Origem;Campanha;Leads novos;Entradas;Em andamento;Ganhos;Perdidos;Taxa de conversão;Mediana do primeiro contato (min)\r\n"
            + "Meta;Outubro | Serviço Y;3;4;1;1;1;33,3%;12,5\r\n"
            + "Google Ads;Google · campanha 123;0;1;0;0;0;;\r\n"
            + "Site;Sem campanha (não veio de anúncio);1;2;1;0;0;0%;\r\n"
            + "Total;;4;7;2;1;1;25%;12,5\r\n",
            Texto(relatorio));
    }

    [Fact]
    public void Sem_linhas_sai_o_cabecalho_e_o_total_zerado()
    {
        var relatorio = new RelatorioCampanhasResponse(De, Ate, [], new TotalRelatorioResponse(0, 0, 0, 0, 0, null, null));

        Assert.Equal(
            "Origem;Campanha;Leads novos;Entradas;Em andamento;Ganhos;Perdidos;Taxa de conversão;Mediana do primeiro contato (min)\r\n"
            + "Total;;0;0;0;0;0;;\r\n",
            Texto(relatorio));
    }

    [Fact]
    public void Campanha_que_o_Excel_leria_como_formula_ganha_o_apostrofo()
    {
        var relatorio = new RelatorioCampanhasResponse(
            De,
            Ate,
            [new(Origem.MetaLeadAds, new CampanhaResumo(1, "-20% Black Friday"), 1, 1, 1, 0, 0, 0, null)],
            new TotalRelatorioResponse(1, 1, 1, 0, 0, 0, null));

        Assert.Contains("\r\nMeta;'-20% Black Friday;1;", Texto(relatorio), StringComparison.Ordinal);
    }

    [Fact]
    public void Taxa_e_mediana_tem_ate_uma_casa_com_virgula()
    {
        Assert.Equal(["66,7%", "100%", "12,5%", "0%", ""], new double?[] { 2.0 / 3, 1, 0.125, 0, null }.Select(RelatorioCampanhasCsv.Porcentagem));
        Assert.Equal(["0", "60", "25", "12,5", ""], new double?[] { 0.04, 59.96, 25, 12.46, null }.Select(RelatorioCampanhasCsv.Minutos));
    }

    [Fact]
    public void Nome_do_arquivo_tem_o_periodo()
    {
        var relatorio = new RelatorioCampanhasResponse(De, Ate, [], new TotalRelatorioResponse(0, 0, 0, 0, 0, null, null));

        Assert.Equal("relatorio-campanhas-2026-09-01-a-2026-09-30.csv", RelatorioCampanhasCsv.NomeArquivo(relatorio));
    }
}
