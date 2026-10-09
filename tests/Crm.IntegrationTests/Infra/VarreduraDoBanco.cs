using Npgsql;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Procura um texto em todas as colunas de texto, JSON, lista e bytes de todas as tabelas do banco: a prova de que um dado
/// pessoal saiu de verdade (critério de aceite do Passo 10), e não só dos lugares que o código lembra de limpar.
/// </summary>
public static class VarreduraDoBanco
{
    /// <summary>As colunas, como <c>tabela.coluna</c>, em que o texto aparece, sem diferenciar maiúsculas nos textos.</summary>
    public static async Task<List<string>> OndeApareceAsync(string banco, string texto)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexao = new NpgsqlConnection(banco);
        await conexao.OpenAsync(ct);

        var colunas = new List<(string Tabela, string Coluna, string Tipo)>();
        await using (var comando = new NpgsqlCommand(
            """
            SELECT table_name, column_name, data_type FROM information_schema.columns
            WHERE table_schema = 'public' AND data_type IN ('text', 'character varying', 'json', 'jsonb', 'ARRAY', 'bytea')
            ORDER BY table_name, column_name
            """,
            conexao))
        await using (var leitor = await comando.ExecuteReaderAsync(ct))
        {
            while (await leitor.ReadAsync(ct))
            {
                colunas.Add((leitor.GetString(0), leitor.GetString(1), leitor.GetString(2)));
            }
        }

        var achadas = new List<string>();
        foreach (var (tabela, coluna, tipo) in colunas)
        {
            // Os bytes, como o arquivo guardado da importação, são comparados como estão: os marcadores dos testes só têm
            // ASCII, que sai igual em UTF-8 e no Windows-1252 do Excel.
            var bytes = tipo == "bytea";
            var condicao = bytes
                ? $"position(convert_to(@texto, 'UTF8') in \"{coluna}\") > 0"
                : $"\"{coluna}\"::text ILIKE '%' || @texto || '%'";
            await using var busca = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM \"{tabela}\" WHERE {condicao})", conexao);
            busca.Parameters.AddWithValue("texto", bytes ? texto : EscaparLike(texto));
            if ((bool)(await busca.ExecuteScalarAsync(ct))!)
            {
                achadas.Add($"{tabela}.{coluna}");
            }
        }

        return achadas;
    }

    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
