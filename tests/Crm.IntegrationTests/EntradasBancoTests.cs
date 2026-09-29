using Crm.IntegrationTests.Infra;
using Npgsql;

namespace Crm.IntegrationTests;

/// <summary>Regras garantidas pelo próprio banco. Cada teste roda numa transação desfeita no fim.</summary>
public class EntradasBancoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task InserirEntradaAsync(NpgsqlConnection conexao, NpgsqlTransaction transacao, string idExterno, string situacao)
    {
        await using var comando = new NpgsqlCommand(
            """
            INSERT INTO entradas_recebidas (origem, id_externo, situacao, teste, recebido_em, processado_em)
            VALUES ('meta_lead_ads', @idExterno, @situacao, false, now(), now())
            """,
            conexao,
            transacao);
        comando.Parameters.AddWithValue("idExterno", idExterno);
        comando.Parameters.AddWithValue("situacao", situacao);
        await comando.ExecuteNonQueryAsync(Ct);
    }

    [Fact]
    public async Task A_mesma_origem_e_id_externo_nao_entram_duas_vezes()
    {
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var transacao = await conexao.BeginTransactionAsync(Ct);
        var idExterno = Guid.NewGuid().ToString();
        await InserirEntradaAsync(conexao, transacao, idExterno, "criado");

        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirEntradaAsync(conexao, transacao, idExterno, "vinculado"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, erro.SqlState);
    }

    [Fact]
    public async Task Entrada_com_erro_nao_impede_o_reenvio()
    {
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var transacao = await conexao.BeginTransactionAsync(Ct);
        var idExterno = Guid.NewGuid().ToString();

        await InserirEntradaAsync(conexao, transacao, idExterno, "erro");
        await InserirEntradaAsync(conexao, transacao, idExterno, "erro");
        await InserirEntradaAsync(conexao, transacao, idExterno, "criado");
    }
}
