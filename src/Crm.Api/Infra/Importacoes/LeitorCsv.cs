using System.Text;
using System.Text.Json.Serialization;

namespace Crm.Api.Infra.Importacoes;

/// <summary>Separador das colunas do arquivo da importação (seção 3.7). Os valores começam em 1.</summary>
public enum Separador
{
    [JsonStringEnumMemberName("virgula")]
    Virgula = 1,

    [JsonStringEnumMemberName("ponto_e_virgula")]
    PontoEVirgula = 2,

    [JsonStringEnumMemberName("tab")]
    Tab = 3,
}

/// <summary>Um registro do CSV, com os campos como vieram.</summary>
/// <param name="Numero">A linha do registro na planilha, como o Excel numera: o primeiro registro é a linha 1.</param>
/// <param name="AspasSemFechamento">O último campo abriu aspas, e o arquivo acabou antes de elas fecharem.</param>
internal sealed record RegistroCsv(int Numero, IReadOnlyList<string> Campos, bool AspasSemFechamento);

/// <summary>
/// Lê um CSV no formato da RFC 4180: um campo entre aspas pode ter o separador, quebras de linha e aspas dobradas
/// (<c>""</c>). Aceita as quebras de linha do Windows, do Linux e do Mac antigo, e a linha <c>sep=;</c> que o Excel
/// às vezes põe no início para dizer o separador.
/// </summary>
internal static class LeitorCsv
{
    private const char Aspas = '"';

    /// <summary>Os separadores aceitos, na ordem de desempate: o tab quase nunca aparece no meio de um texto.</summary>
    private static readonly (char Caractere, Separador Separador)[] Separadores =
    [
        ('\t', Separador.Tab),
        (';', Separador.PontoEVirgula),
        (',', Separador.Virgula),
    ];

    /// <summary>
    /// Os registros do texto e o separador. O separador vem da linha <c>sep=</c> ou é o que mais aparece fora de
    /// aspas no primeiro registro, o cabeçalho. Nulo quando o cabeçalho não tem nenhum dos separadores aceitos.
    /// </summary>
    public static (Separador Separador, List<RegistroCsv> Registros)? Ler(string texto)
    {
        var inicio = texto.StartsWith('﻿') ? 1 : 0;
        if ((LerDiretiva(texto, ref inicio) ?? DetectarSeparador(texto, inicio)) is not { } separador)
        {
            return null;
        }

        return (Separadores.Single(s => s.Caractere == separador).Separador, LerRegistros(texto, inicio, separador));
    }

    /// <summary>A linha <c>sep=;</c> do Excel, que não aparece na planilha: devolve o separador e pula a linha.</summary>
    private static char? LerDiretiva(string texto, ref int inicio)
    {
        var tamanho = texto.AsSpan(inicio).IndexOfAny('\r', '\n');
        var fim = tamanho < 0 ? texto.Length : inicio + tamanho;
        var linha = texto[inicio..fim].Trim(' ', Aspas);
        if (linha.Length != 5
            || !linha.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)
            || !Separadores.Any(s => s.Caractere == linha[4]))
        {
            return null;
        }

        inicio = fim < texto.Length ? PularQuebra(texto, fim) : fim;
        return linha[4];
    }

    /// <summary>O separador que mais aparece fora de aspas no primeiro registro. Nulo se nenhum aparece.</summary>
    private static char? DetectarSeparador(string texto, int inicio)
    {
        var contagem = new Dictionary<char, int>();
        var entreAspas = false;
        for (var i = inicio; i < texto.Length; i++)
        {
            var c = texto[i];
            if (c == Aspas)
            {
                entreAspas = !entreAspas;
            }
            else if (!entreAspas && c is '\r' or '\n')
            {
                break;
            }
            else if (!entreAspas && Separadores.Any(s => s.Caractere == c))
            {
                contagem[c] = contagem.GetValueOrDefault(c) + 1;
            }
        }

        var maior = contagem.Values.DefaultIfEmpty(0).Max();
        return maior == 0 ? null : Separadores.First(s => contagem.GetValueOrDefault(s.Caractere) == maior).Caractere;
    }

    private static List<RegistroCsv> LerRegistros(string texto, int inicio, char separador)
    {
        var registros = new List<RegistroCsv>();
        var campo = new StringBuilder();
        var i = inicio;
        while (i < texto.Length)
        {
            var campos = new List<string>();
            var aspasSemFechamento = false;
            while (true)
            {
                campo.Clear();
                if (i < texto.Length && texto[i] == Aspas)
                {
                    i++;
                    aspasSemFechamento = !LerEntreAspas(texto, ref i, campo);
                }

                // O campo sem aspas, ou o que sobra depois de fechá-las: um campo malformado fica como veio.
                while (i < texto.Length && texto[i] != separador && texto[i] is not ('\r' or '\n'))
                {
                    campo.Append(texto[i]);
                    i++;
                }

                campos.Add(campo.ToString());
                if (i < texto.Length && texto[i] == separador)
                {
                    i++;
                    continue;
                }

                break;
            }

            if (i < texto.Length)
            {
                i = PularQuebra(texto, i);
            }

            registros.Add(new RegistroCsv(registros.Count + 1, campos, aspasSemFechamento));
        }

        return registros;
    }

    /// <summary>
    /// Lê um campo entre aspas, a partir do caractere depois da aspa que abre. Falso quando o texto acaba antes de
    /// as aspas fecharem.
    /// </summary>
    private static bool LerEntreAspas(string texto, ref int i, StringBuilder campo)
    {
        while (i < texto.Length)
        {
            var c = texto[i];
            i++;
            if (c != Aspas)
            {
                campo.Append(c);
            }
            else if (i < texto.Length && texto[i] == Aspas)
            {
                campo.Append(Aspas);
                i++;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A posição depois da quebra de linha em <paramref name="i"/>: CRLF, LF ou CR.</summary>
    private static int PularQuebra(string texto, int i) =>
        texto[i] == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n' ? i + 2 : i + 1;
}
