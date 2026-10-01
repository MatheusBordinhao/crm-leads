using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;

namespace Crm.Api.Infra.Web;

internal static class ChavesDaSessaoExtensions
{
    /// <summary>Nome fixo nas chaves: o cookie continua valendo mesmo que a pasta da API mude de um deploy para outro.</summary>
    public const string NomeDaAplicacao = "crm-leads";

    /// <summary>
    /// Chaves do Data Protection, que protegem o cookie da sessão. Com <c>DataProtection__Pasta</c>, elas ficam nessa
    /// pasta, que em produção é um volume do Docker. Sem isso, as chaves ficam dentro do contêiner, e todo mundo perde
    /// a sessão quando ele é recriado.
    /// </summary>
    public static IServiceCollection AddChavesDaSessao(this IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName(NomeDaAplicacao);

        // Lida quando as opções são montadas, e não na subida: assim os testes conseguem trocar a pasta.
        services.AddOptions<KeyManagementOptions>().Configure<IConfiguration, ILoggerFactory>((opcoes, configuracao, logs) =>
        {
            if (configuracao["DataProtection:Pasta"] is { Length: > 0 } pasta)
            {
                opcoes.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(pasta), logs);
            }
        });
        return services;
    }
}
