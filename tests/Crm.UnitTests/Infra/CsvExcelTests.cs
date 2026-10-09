using System.Text;
using Crm.Api.Infra.Web;

namespace Crm.UnitTests.Infra;

public class CsvExcelTests
{
    /// <summary>O texto do arquivo, depois de conferir o BOM do UTF-8 na frente.</summary>
    private static string Texto(CsvExcel csv)
    {
        var bytes = csv.ParaBytes();
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        return Encoding.UTF8.GetString(bytes[3..]);
    }

    [Fact]
    public void Linhas_com_ponto_e_virgula_e_CRLF_depois_do_BOM()
    {
        var csv = new CsvExcel().Linha("Campanha", "Leads novos").Linha("Outubro | Serviço Y", "4");

        Assert.Equal("Campanha;Leads novos\r\nOutubro | Serviço Y;4\r\n", Texto(csv));
    }

    [Fact]
    public void Nulo_e_vazio_viram_campo_vazio()
    {
        Assert.Equal(";;Total\r\n", Texto(new CsvExcel().Linha(null, "", "Total")));
    }

    [Fact]
    public void Campo_com_ponto_e_virgula_aspas_ou_quebra_de_linha_vai_entre_aspas()
    {
        Assert.Equal("\"Outubro; Serviço Y\"", CsvExcel.Campo("Outubro; Serviço Y"));
        Assert.Equal("\"O \"\"melhor\"\" serviço\"", CsvExcel.Campo("O \"melhor\" serviço"));
        Assert.Equal("\"Primeira linha\nSegunda linha\"", CsvExcel.Campo("Primeira linha\nSegunda linha"));
        Assert.Equal("\"Primeira linha\r\nSegunda linha\"", CsvExcel.Campo("Primeira linha\r\nSegunda linha"));
    }

    /// <summary>
    /// Conferido no Excel: sem o apóstrofo, "+Leads outubro" vira #NOME?, "=1+1" vira 2 e "-2+3" vira 1. O CSV não tenta
    /// adivinhar o que o Excel lê como fórmula: todo texto que começa com esses sinais ganha o apóstrofo.
    /// </summary>
    [Theory]
    [InlineData("+Leads outubro", "'+Leads outubro")]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("-20% Black Friday", "'-20% Black Friday")]
    [InlineData("@campanha", "'@campanha")]
    [InlineData("\tcom tab", "'\tcom tab")]
    public void Texto_que_comeca_como_formula_ganha_um_apostrofo(string valor, string esperado) =>
        Assert.Equal(esperado, CsvExcel.Campo(valor));

    [Fact]
    public void Formula_com_ponto_e_virgula_ganha_o_apostrofo_dentro_das_aspas()
    {
        Assert.Equal("\"'@SOMA(1;2)\"", CsvExcel.Campo("@SOMA(1;2)"));
        Assert.Equal("\"'=HIPERLINK(\"\"x\"\";\"\"y\"\")\"", CsvExcel.Campo("=HIPERLINK(\"x\";\"y\")"));
    }

    [Fact]
    public void Sinal_no_meio_do_texto_e_numeros_ficam_como_estao()
    {
        Assert.Equal("Black Friday -20%", CsvExcel.Campo("Black Friday -20%"));
        Assert.Equal("33,3%", CsvExcel.Campo("33,3%"));
        Assert.Equal("12,5", CsvExcel.Campo("12,5"));
    }
}
