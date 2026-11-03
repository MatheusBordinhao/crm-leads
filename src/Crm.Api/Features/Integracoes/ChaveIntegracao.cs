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

    /// <summary>
    /// A chave de antes, que vale junto com a atual só durante a troca (README › Trocar chaves e senhas). Vazia ou
    /// ausente, só a atual vale.
    /// </summary>
    public string? ChaveAnterior { get; set; }

    public int RequisicoesPorMinuto { get; set; } = 120;
}

/// <summary>
/// Autentica o n8n pelo cabeçalho <c>X-Api-Key</c>. A chave recebida é comparada com a atual e com a anterior, sempre
/// as duas, pelo SHA-256, que tem sempre o mesmo tamanho, e em tempo constante: o tempo de resposta não diz qual valeu.
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

    /// <summary>O começo do aviso de cada uso da anterior, sem acento, para a busca no log em JSON da produção.</summary>
    public const string AvisoUso = "Uso da Integracao__ChaveAnterior";

    /// <summary>O começo do aviso da subida enquanto a anterior está preenchida, também sem acento.</summary>
    public const string AvisoTroca = "Troca da chave em andamento";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var chaves = integracao.Value;
        if (string.IsNullOrEmpty(chaves.Chave))
        {
            return Task.FromResult(AuthenticateResult.Fail("A chave de integração não está configurada."));
        }

        if (!Request.Headers.TryGetValue(Cabecalho, out var recebidas) || recebidas.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Sem a anterior, a segunda comparação repete a atual e não conta: nenhuma comparação é feita com uma chave vazia,
        // que tem o mesmo SHA-256 de um cabeçalho vazio.
        var anterior = string.IsNullOrEmpty(chaves.ChaveAnterior) ? null : chaves.ChaveAnterior;
        var recebida = Hash(recebidas[0] ?? string.Empty);
        var valeAtual = CryptographicOperations.FixedTimeEquals(recebida, Hash(chaves.Chave));
        var valeAnterior = CryptographicOperations.FixedTimeEquals(recebida, Hash(anterior ?? chaves.Chave)) & (anterior is not null);
        if (!(valeAtual | valeAnterior))
        {
            return Task.FromResult(AuthenticateResult.Fail("Chave de integração inválida."));
        }

        if (valeAnterior)
        {
            Logger.LogWarning(
                AvisoUso + ": a integração recebeu a chave anterior. Troque a credencial do n8n e depois rode a fase 2"
                + " (README › Trocar chaves e senhas)");
        }

        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, "n8n")], Esquema);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }

    private static byte[] Hash(string chave) => SHA256.HashData(Encoding.UTF8.GetBytes(chave));
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
            .Validate(
                o => string.IsNullOrEmpty(o.ChaveAnterior) || o.ChaveAnterior.Length >= IntegracaoOptions.TamanhoMinimoChave,
                $"Integracao__ChaveAnterior precisa ter pelo menos {IntegracaoOptions.TamanhoMinimoChave} caracteres (veja o README).")
            .Validate(
                o => string.IsNullOrEmpty(o.ChaveAnterior) || !string.IsNullOrEmpty(o.Chave),
                "Integracao__ChaveAnterior só vale com a Integracao__Chave preenchida (veja o README › Trocar chaves e senhas).")
            .Validate(
                o => string.IsNullOrEmpty(o.ChaveAnterior) || o.ChaveAnterior != o.Chave,
                "Integracao__ChaveAnterior não pode ser igual à Integracao__Chave (veja o README › Trocar chaves e senhas).")
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

    /// <summary>
    /// Enquanto a anterior estiver preenchida, a subida avisa no log, sem a chave: depois da fase 2 da troca, o aviso some.
    /// </summary>
    public static void AvisarDaTrocaDaChave(this WebApplication app)
    {
        if (!string.IsNullOrEmpty(app.Services.GetRequiredService<IOptions<IntegracaoOptions>>().Value.ChaveAnterior))
        {
            app.Services.GetRequiredService<ILogger<ChaveIntegracaoHandler>>().LogWarning(
                ChaveIntegracaoHandler.AvisoTroca + ": a Integracao__ChaveAnterior ainda vale. Troque a credencial do n8n e depois"
                + " rode a fase 2 (README › Trocar chaves e senhas)");
        }
    }
}
