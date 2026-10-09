using System.Text;

namespace Crm.Api.Infra.Web;

/// <summary>
/// CSV para o Excel brasileiro abrir com um clique duplo: UTF-8 com BOM (sem ele, o Excel lê os acentos como
/// Windows-1252), ponto e vírgula, que é o separador do Excel em português, e CRLF.
/// </summary>
internal sealed class CsvExcel
{
    public const string TipoConteudo = "text/csv; charset=utf-8";

    private readonly StringBuilder _texto = new();

    /// <summary>Uma linha, com os campos na ordem. Nulo vira um campo vazio.</summary>
    public CsvExcel Linha(params string?[] campos)
    {
        _texto.AppendJoin(';', campos.Select(Campo)).Append("\r\n");
        return this;
    }

    /// <summary>O arquivo, com o BOM do UTF-8 na frente.</summary>
    public byte[] ParaBytes() => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(_texto.ToString())];

    /// <summary>
    /// O campo como vai no arquivo. Um texto que começa com =, +, - ou @ (ou com tab ou CR) ganha um apóstrofo na
    /// frente, que o Excel mostra na célula. Sem ele, o Excel pode ler o campo como fórmula: conferido no Excel,
    /// uma campanha "+Leads outubro" vira #NOME?, "=1+1" e "-2+3" viram contas, e "@SOMA(1;2)" vira =SOMA(1;2).
    /// Depois, o campo vai entre aspas se tiver ponto e vírgula, aspas ou quebra de linha.
    /// </summary>
    public static string Campo(string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return string.Empty;
        }

        if (valor[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            valor = "'" + valor;
        }

        return valor.AsSpan().IndexOfAny(";\"\r\n") < 0 ? valor : $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
