using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>Primeiro admin, criado na subida. Os testes não mudam a senha nem os dados dele.</summary>
    public const string AdminInicialEmail = "admin-inicial@example.com";

    public const string AdminInicialSenha = "senha-do-admin-inicial";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(ImagemPostgres).Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // As migrations vêm antes da subida da API, porque a subida já cria os perfis e o primeiro admin.
        await using (var db = new CrmDbContext(DadosExtensions.CriarOpcoes(ConnectionString)))
        {
            await db.Database.MigrateAsync();
        }

        // Sobe a API agora, para a criação dos perfis acontecer uma vez só, antes de qualquer teste.
        _ = Services;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(AmbienteTestes);
        builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", ConnectionString);
        builder.UseSetting("Admin:Email", AdminInicialEmail);
        builder.UseSetting("Admin:SenhaInicial", AdminInicialSenha);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
