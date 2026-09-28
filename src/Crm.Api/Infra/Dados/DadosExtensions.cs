using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Dados;

internal static class DadosExtensions
{
    public const string NomeConexao = "Crm";

    /// <summary>
    /// Registra o <see cref="CrmDbContext"/>. A connection string é lida quando o contexto é criado, para os
    /// testes poderem trocá-la e o <c>dotnet ef</c> funcionar mesmo sem ela.
    /// </summary>
    public static IServiceCollection AddCrmDbContext(this IServiceCollection services) =>
        services.AddDbContext<CrmDbContext>((provedor, opcoes) =>
        {
            var conexao = provedor.GetRequiredService<IConfiguration>().GetConnectionString(NomeConexao);
            var npgsql = string.IsNullOrWhiteSpace(conexao) ? opcoes.UseNpgsql() : opcoes.UseNpgsql(conexao);
            npgsql.UseSnakeCaseNamingConvention();
        });

    /// <summary>Falha na inicialização, com uma mensagem clara, se a connection string não estiver configurada.</summary>
    public static void ExigirConnectionString(this IConfiguration configuracao)
    {
        if (string.IsNullOrWhiteSpace(configuracao.GetConnectionString(NomeConexao)))
        {
            throw new InvalidOperationException("Configure ConnectionStrings__Crm no .env (veja o .env.example).");
        }
    }

    public static async Task AplicarMigrationsAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Database.MigrateAsync();
    }
}
