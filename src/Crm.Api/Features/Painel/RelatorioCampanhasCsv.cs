using System.Globalization;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Web;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Painel;

/// <summary>
/// O relatório por campanha em CSV para o Excel (<see cref="CsvExcel"/>): o mesmo arquivo na tela do admin e no e-mail
/// da Gama. A taxa vai como porcentagem e a mediana em minutos, com até uma casa e vírgula decimal, que o Excel em
/// português lê como número. Sem valor, o campo fica vazio.
/// </summary>
internal static class RelatorioCampanhasCsv
{
    /// <summary>O rótulo das linhas sem campanha, o mesmo do painel.</summary>
    public const string SemCampanha = "Sem campanha (não veio de anúncio)";

    public static readonly string[] Cabecalho =
    [
        "Origem",
        "Campanha",
        "Leads novos",
        "Entradas",
        "Em andamento",
        "Ganhos",
        "Perdidos",
        "Taxa de conversão",
        "Mediana do primeiro contato (min)",
    ];

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>O arquivo para a resposta, com o período no nome.</summary>
    public static FileContentResult Arquivo(RelatorioCampanhasResponse relatorio) =>
        new(Gerar(relatorio), CsvExcel.TipoConteudo) { FileDownloadName = NomeArquivo(relatorio) };

    public static byte[] Gerar(RelatorioCampanhasResponse relatorio)
    {
        var csv = new CsvExcel().Linha(Cabecalho);
        foreach (var linha in relatorio.Linhas)
        {
            csv.Linha(
                Origens.NomeLegivel(linha.Origem),
                linha.Campanha?.Nome ?? SemCampanha,
                Inteiro(linha.LeadsNovos),
                Inteiro(linha.Entradas),
                Inteiro(linha.EmAndamento),
                Inteiro(linha.Ganhos),
                Inteiro(linha.Perdidos),
                Porcentagem(linha.TaxaConversao),
                Minutos(linha.MedianaPrimeiroContatoMinutos));
        }

        var total = relatorio.Total;
        csv.Linha(
            "Total",
            null,
            Inteiro(total.LeadsNovos),
            Inteiro(total.Entradas),
            Inteiro(total.EmAndamento),
            Inteiro(total.Ganhos),
            Inteiro(total.Perdidos),
            Porcentagem(total.TaxaConversao),
            Minutos(total.MedianaPrimeiroContatoMinutos));
        return csv.ParaBytes();
    }

    /// <summary><c>relatorio-campanhas-2026-09-01-a-2026-09-30.csv</c></summary>
    public static string NomeArquivo(RelatorioCampanhasResponse relatorio) =>
        $"relatorio-campanhas-{Dia(relatorio.De)}-a-{Dia(relatorio.Ate)}.csv";

    /// <summary>Uma taxa de 0 a 1 como porcentagem, com até uma casa: <c>33,3%</c>. Sem taxa, vazio.</summary>
    public static string Porcentagem(double? taxa) => taxa is { } valor ? $"{Decimal(valor * 100)}%" : string.Empty;

    /// <summary>Minutos com até uma casa: <c>12,5</c>. Sem mediana, vazio.</summary>
    public static string Minutos(double? minutos) => minutos is { } valor ? Decimal(valor) : string.Empty;

    private static string Decimal(double valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero).ToString("0.#", PtBr);

    private static string Inteiro(int valor) => valor.ToString(CultureInfo.InvariantCulture);

    private static string Dia(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
