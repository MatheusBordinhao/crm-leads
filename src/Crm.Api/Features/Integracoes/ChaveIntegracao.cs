using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Crm.Api.Features.Integracoes;

/// <summary>Chave e limite da integração, na seção <c>Integracao</c> da configuração.</summary>
internal sealed class IntegracaoOptions
{
    public const string Secao = "Integracao";

    public const int TamanhoMinimoChave = 32;

    /// <summary>Chave que o n8n manda em <c>X-Api-Key</c>. Vazia, a integração fica desligada e responde 401.</summary>
    public string? Chave { get; set; }

    public int RequisicoesPorMinuto { get; set; } = 120;
}

/// <summary>
/// Autentica o n8n pelo cabeçalho <c>X-Api-Key</c>. A comparação é feita sobre o SHA-256 das duas chaves, que
/// têm sempre o mesmo tamanho, em tempo constante.
/// </summary>
internal sealed class ChaveIntegracaoHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> opcoes,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<IntegracaoOptions> integracao)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opcoes, logger, encoder)
{
    public const string Esquema = "ChaveIntegracao";

    public const string Cabecalho = "X-Api-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var esperada = integracao.Value.Chave;
        if (string.IsNullOrEmpty(esperada))
        {
            return Task.FromResult(AuthenticateResult.Fail("A chave de integração não está configurada."));
        }

        if (!Request.Headers.TryGetValue(Cabecalho, out var recebidas) || recebidas.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Iguais(recebidas[0] ?? string.Empty, esperada))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de integração inválida."));
        }

        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, "n8n")], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }

    private static bool Iguais(string recebida, string esperada) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(recebida)),
            SHA256.HashData(Encoding.UTF8.GetBytes(esperada)));
}

internal static class IntegracaoExtensions
{
    public const string PoliticaLimite = "integracao";

    /// <summary>Esquema da chave de integração e o limite de requisições por minuto (seção 4.2).</summary>
    public static IServiceCollection AddIntegracoes(this IServiceCollection services)
    {
        services.AddOptions<IntegracaoOptions>()
            .BindConfiguration(IntegracaoOptions.Secao)
            .Validate(
                o => string.IsNullOrEmpty(o.Chave) || o.Chave.Length >= IntegracaoOptions.TamanhoMinimoChave,
                $"Integracao__Chave precisa ter pelo menos {IntegracaoOptions.TamanhoMinimoChave} caracteres (veja o README).")
            .ValidateOnStart();

        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ChaveIntegracaoHandler>(ChaveIntegracaoHandler.Esquema, configureOptions: null);

        // Uma partição só: o n8n é o único cliente dessas rotas.
        services.AddRateLimiter(opcoes => opcoes.AddPolicy(PoliticaLimite, contexto =>
        {
            var limite = contexto.RequestServices.GetRequiredService<IOptions<IntegracaoOptions>>().Value.RequisicoesPorMinuto;
            return RateLimitPartition.GetFixedWindowLimiter(PoliticaLimite, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });
        }));

        return services;
    }
}
