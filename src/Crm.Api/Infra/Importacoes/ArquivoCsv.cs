using System.Diagnostics.CodeAnalysis;
using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.Api.Infra.Importacoes;

/// <summary>Uma linha de dados do arquivo, com um valor para cada coluna do cabeçalho.</summary>
/// <param name="Numero">A linha na planilha, como o Excel mostra: o cabeçalho é a linha 1.</param>
/// <param name="Erro">Problema na estrutura da linha. A linha vira um erro da importação, com esse motivo.</param>
public sealed record LinhaCsv(int Numero, IReadOnlyList<string> Valores, string? Erro);

/// <summary>O arquivo lido: como ele foi lido, os nomes das colunas e as linhas que têm algum valor.</summary>
public sealed record TabelaCsv(
    Codificacao Codificacao, Separador Separador, IReadOnlyList<string> Colunas, IReadOnlyList<LinhaCsv> Linhas);

/// <summary>O arquivo lido ou, quando ele é recusado inteiro, o motivo.</summary>
public sealed record LeituraCsv(TabelaCsv? Tabela, string? Erro)
{
    [MemberNotNullWhen(true, nameof(Tabela))]
    [MemberNotNullWhen(false, nameof(Erro))]
    public bool Lido => Tabela is not null;
}

/// <summary>
/// Lê o arquivo da importação (seção 3.7): até 5 MB e 5.000 linhas, em UTF-8, UTF-16 ou Windows-1252, com as colunas
/// separadas por vírgula, ponto e vírgula ou tab. As linhas vazias são ignoradas, mas contam na numeração, como no
/// Excel, para o número da linha de um erro ser o mesmo da planilha.
/// </summary>
public static class ArquivoCsv
{
    public const int TamanhoMaximo = 5 * 1024 * 1024;

    public const int MaximoDeLinhas = 5_000;

    public const int MaximoDeColunas = 100;

    public const string MensagemVazio = "O arquivo está vazio.";

    public const string MensagemTamanho = "O arquivo passa de 5 MB. Divida-o em arquivos menores.";

    public const string MensagemPlanilhaDoExcel =
        "Esse arquivo é uma planilha do Excel, e não um CSV. No Excel, use Arquivo › Salvar como e escolha o tipo CSV.";

    public const string MensagemNaoECsv = "O arquivo não é um CSV.";

    public const string MensagemSemColunas =
        "Não encontrei as colunas. A primeira linha precisa ter o cabeçalho, com pelo menos duas colunas separadas por vírgula, ponto e vírgula ou tab.";

    public const string MensagemColunas = "O arquivo tem mais de 100 colunas.";

    public const string MensagemSemLinhas = "O arquivo não tem nenhuma linha depois do cabeçalho.";

    public const string MensagemLinhas = "O arquivo tem mais de 5.000 linhas. Divida-o em arquivos menores.";

    public const string ErroColunasAMais = "A linha tem mais colunas que o cabeçalho.";

    public const string ErroAspas = "A linha abre aspas e não as fecha.";

    /// <summary>Início de um .xlsx (um zip).</summary>
    private static ReadOnlySpan<byte> InicioXlsx => [0x50, 0x4B, 0x03, 0x04];

    /// <summary>Início de um .xls antigo.</summary>
    private static ReadOnlySpan<byte> InicioXls => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static LeituraCsv Ler(ReadOnlySpan<byte> conteudo)
    {
        if (conteudo.IsEmpty)
        {
            return Recusado(MensagemVazio);
        }

        if (conteudo.Length > TamanhoMaximo)
        {
            return Recusado(MensagemTamanho);
        }

        if (conteudo.StartsWith(InicioXlsx) || conteudo.StartsWith(InicioXls))
        {
            return Recusado(MensagemPlanilhaDoExcel);
        }

        var (texto, codificacao) = Codificacoes.Decodificar(conteudo);
        if (texto.Contains('\0'))
        {
            return Recusado(MensagemNaoECsv);
        }

        if (LeitorCsv.Ler(texto) is not (var separador, { Count: > 0 } registros))
        {
            return Recusado(MensagemSemColunas);
        }

        var colunas = NomesDasColunas(registros[0].Campos);
        if (colunas.Count < 2)
        {
            return Recusado(MensagemSemColunas);
        }

        if (colunas.Count > MaximoDeColunas)
        {
            return Recusado(MensagemColunas);
        }

        var linhas = new List<LinhaCsv>();
        foreach (var registro in registros.Skip(1).Where(r => !r.Campos.All(string.IsNullOrWhiteSpace)))
        {
            if (linhas.Count == MaximoDeLinhas)
            {
                return Recusado(MensagemLinhas);
            }

            linhas.Add(Linha(registro, colunas.Count));
        }

        return linhas.Count == 0
            ? Recusado(MensagemSemLinhas)
            : new LeituraCsv(new TabelaCsv(codificacao, separador, colunas, linhas), null);
    }

    private static LeituraCsv Recusado(string motivo) => new(null, motivo);

    /// <summary>
    /// Os nomes das colunas, sem espaços nas pontas. As colunas vazias do fim (o Excel deixa separadores sobrando) saem;
    /// uma vazia no meio vira "Coluna N", e um nome repetido ganha um número: "Telefone (2)".
    /// </summary>
    private static List<string> NomesDasColunas(IReadOnlyList<string> cabecalho)
    {
        var campos = cabecalho.Select(Textos.Limpar).ToList();
        while (campos.Count > 0 && campos[^1] is null)
        {
            campos.RemoveAt(campos.Count - 1);
        }

        var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nomes = new List<string>(campos.Count);
        for (var i = 0; i < campos.Count; i++)
        {
            var nome = campos[i] ?? $"Coluna {i + 1}";
            var unico = nome;
            for (var vez = 2; !usados.Add(unico); vez++)
            {
                unico = $"{nome} ({vez})";
            }

            nomes.Add(unico);
        }

        return nomes;
    }

    /// <summary>Um valor por coluna: os que faltam ficam vazios, e um valor numa coluna a mais vira erro da linha.</summary>
    private static LinhaCsv Linha(RegistroCsv registro, int colunas)
    {
        var valores = registro.Campos.Take(colunas).ToList();
        while (valores.Count < colunas)
        {
            valores.Add(string.Empty);
        }

        var erro = registro.AspasSemFechamento ? ErroAspas
            : registro.Campos.Skip(colunas).Any(c => !string.IsNullOrWhiteSpace(c)) ? ErroColunasAMais
            : null;
        return new LinhaCsv(registro.Numero, valores, erro);
    }
}
