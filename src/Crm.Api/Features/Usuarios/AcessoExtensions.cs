using System.Globalization;
using System.Threading.RateLimiting;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Crm.Api.Features.Usuarios;

/// <summary>Limite de requisições do acesso, na seção <c>Acesso</c> da configuração.</summary>
internal sealed class AcessoOptions
{
    public const string Secao = "Acesso";

    /// <summary>Tentativas de login por minuto, por IP.</summary>
    public int LoginsPorMinuto { get; set; } = 10;
}

internal static class AcessoExtensions
{
    public const string NomeCookie = "crm_sessao";

    public const string PoliticaLimiteLogin = "login";

    public const int TamanhoMinimoSenha = 10;

    public const int TentativasAteBloqueio = 5;

    public const int MinutosDeBloqueio = 15;

    /// <summary>
    /// Identity com os perfis, a política de senha e de bloqueio, o cookie da seção 9, as políticas de
    /// autorização e o limite de logins.
    /// </summary>
    public static IServiceCollection AddAcesso(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<AdminInicialOptions>().BindConfiguration(AdminInicialOptions.Secao);
        services.AddOptions<AcessoOptions>().BindConfiguration(AcessoOptions.Secao);
        services.AddScoped<InicializacaoAcesso>();

        services
            .AddIdentityCore<Usuario>(opcoes =>
            {
                // O e-mail é validado à parte; qualquer caractere que ele aceite serve como nome de usuário.
                opcoes.User.AllowedUserNameCharacters = string.Empty;
                opcoes.User.RequireUniqueEmail = true;

                opcoes.Password.RequiredLength = TamanhoMinimoSenha;
                opcoes.Password.RequiredUniqueChars = 1;
                opcoes.Password.RequireDigit = false;
                opcoes.Password.RequireLowercase = false;
                opcoes.Password.RequireUppercase = false;
                opcoes.Password.RequireNonAlphanumeric = false;

                opcoes.Lockout.AllowedForNewUsers = true;
                opcoes.Lockout.MaxFailedAccessAttempts = TentativasAteBloqueio;
                opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(MinutosDeBloqueio);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CrmDbContext>()
            .AddSignInManager<SignInManagerCrm>()
            .AddClaimsPrincipalFactory<ClaimsUsuarioFactory>()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<IdentityErrosPtBr>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(opcoes =>
        {
            opcoes.Cookie.Name = NomeCookie;
            opcoes.Cookie.HttpOnly = true;
            opcoes.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            opcoes.Cookie.SameSite = SameSiteMode.Strict;
            opcoes.ExpireTimeSpan = TimeSpan.FromHours(12);
            opcoes.SlidingExpiration = true;

            // É uma API: responde 401 e 403 em vez de redirecionar para uma página de login.
            opcoes.Events.OnRedirectToLogin = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            opcoes.Events.OnRedirectToAccessDenied = contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        // A cada minuto, a sessão é conferida no banco: usuário desativado, com perfil trocado ou com a senha
        // redefinida perde a sessão ou recebe as permissões novas.
        services.Configure<SecurityStampValidatorOptions>(opcoes => opcoes.ValidationInterval = TimeSpan.FromMinutes(1));

        Politicas.Configurar(services.AddAuthorizationBuilder());
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AutorizacaoResultHandler>();

        services.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.OnRejected = ResponderLimiteAsync;
            opcoes.AddPolicy(PoliticaLimiteLogin, contexto =>
            {
                var limite = contexto.RequestServices.GetRequiredService<IOptions<AcessoOptions>>().Value.LoginsPorMinuto;
                var ip = contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    public static async Task InicializarAcessoAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<InicializacaoAcesso>().ExecutarAsync();
    }

    private static async ValueTask ResponderLimiteAsync(OnRejectedContext contexto, CancellationToken ct)
    {
        var http = contexto.HttpContext;
        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
        {
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Muitas tentativas em pouco tempo. Espere um minuto e tente de novo.",
            },
        });
    }
}
