using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Crm.IntegrationTests.Infra;

/// <summary>Bancos à parte, no mesmo contêiner, para os testes que não podem dividir o banco com os outros.</summary>
public static class BancosDeTeste
{
    /// <summary>Cria um banco com as migrations aplicadas e nenhum usuário, e devolve a connection string dele.</summary>
    public static async Task<string> CriarBancoAsync(this CrmApiFactory fabrica, string prefixo)
    {
        var ct = TestContext.Current.CancellationToken;
        var nome = $"{prefixo}_{Guid.NewGuid():N}";
        await using (var conexao = new NpgsqlConnection(fabrica.ConnectionString))
        {
            await conexao.OpenAsync(ct);
            await using var comando = new NpgsqlCommand($"CREATE DATABASE {nome}", conexao);
            await comando.ExecuteNonQueryAsync(ct);
        }

        var connectionString = new NpgsqlConnectionStringBuilder(fabrica.ConnectionString) { Database = nome }.ToString();
        await using var db = new CrmDbContext(DadosExtensions.CriarOpcoes(connectionString));
        await db.Database.MigrateAsync(ct);
        return connectionString;
    }

    /// <summary>A API num banco à parte. <paramref name="configurar"/> muda o resto, como os serviços em segundo plano.</summary>
    public static WebApplicationFactory<Program> ApiNoBanco(this CrmApiFactory fabrica, string banco, Action<IWebHostBuilder>? configurar = null) =>
        fabrica.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", banco);
            configurar?.Invoke(builder);
        });
}
