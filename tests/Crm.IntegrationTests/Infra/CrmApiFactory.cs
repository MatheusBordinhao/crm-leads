using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CrmApiFactory))]

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// API com um PostgreSQL real, num contêiner compartilhado pela suíte toda. As classes de teste recebem a
/// fábrica pelo construtor.
/// </summary>
public sealed class CrmApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>A mesma imagem do docker-compose.yml.</summary>
    public const string ImagemPostgres = "postgres:17";

    public const string AmbienteTestes = "Testing";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(ImagemPostgres).Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var escopo = Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(AmbienteTestes);
        builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", ConnectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
