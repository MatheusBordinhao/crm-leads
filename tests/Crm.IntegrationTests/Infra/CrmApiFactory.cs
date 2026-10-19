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
    /// <summary>A imagem do PostgreSQL do docker-compose.prod.yml: os testes rodam na mesma versão da produção.</summary>
    public static string ImagemPostgres { get; } = Repositorio.ImagemDoComposeDeProducao("postgres");

    public const string AmbienteTestes = "Testing";

    /// <summary>Primeiro admin, criado na subida. Os testes não mudam a senha nem os dados dele.</summary>
    public const string AdminInicialEmail = "admin-inicial@example.com";

    public const string AdminInicialSenha = "senha-do-admin-inicial";

    /// <summary>Chave de integração (X-Api-Key) dos testes.</summary>
    public const string ChaveIntegracao = "chave-de-integracao-dos-testes-0123456789";

    /// <summary>Base dos links da ficha nos eventos para o n8n.</summary>
    public const string UrlPublica = "https://crm.teste";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(ImagemPostgres).Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // As migrations vêm antes da subida da API, porque a subida já cria os perfis e o primeiro admin.
        await using (var db = new CrmDbContext(DadosExtensions.CriarOpcoes(ConnectionString)))
        {
            await db.Database.MigrateAsync();

            // O contador de LeadsDeTeste (telefone, CPF, marcas de busca) recomeça a cada rodada: só não repete dados de
            // uma rodada anterior porque o contêiner é novo e o banco nasce vazio.
            if (await db.Leads.AnyAsync())
            {
                throw new InvalidOperationException(
                    "O banco dos testes precisa nascer vazio: o contador de LeadsDeTeste recomeça a cada rodada. Não reaproveite o contêiner (WithReuse).");
            }
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
        builder.UseSetting("Integracao:Chave", ChaveIntegracao);
        builder.UseSetting("Crm:UrlPublica", UrlPublica);

        // No TestServer, todas as requisições vêm do mesmo "IP", e a integração tem um contador só. Os limites
        // reais são testados em fábricas à parte.
        builder.UseSetting("Acesso:LoginsPorMinuto", "100000");
        builder.UseSetting("Integracao:RequisicoesPorMinuto", "100000");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
