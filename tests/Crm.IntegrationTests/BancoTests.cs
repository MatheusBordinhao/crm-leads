using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Crm.IntegrationTests;

public class BancoTests(CrmApiFactory fabrica)
{
    [Fact]
    public async Task Extensao_unaccent_esta_instalada()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("SELECT unaccent('Conceição São João')", conexao);

        var resultado = await comando.ExecuteScalarAsync(ct);

        Assert.Equal("Conceicao Sao Joao", resultado);
    }

    [Fact]
    public async Task Todas_as_migrations_foram_aplicadas()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        var pendentes = await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pendentes);
    }

    [Fact]
    public async Task Modelo_nao_tem_mudancas_sem_migration()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        // Se falhar, falta rodar: dotnet ef migrations add <NomeDescritivo> --project src/Crm.Api
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
