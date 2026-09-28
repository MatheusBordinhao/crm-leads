using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.Features.Usuarios;

internal static class AcessoExtensions
{
    public const string NomeCookie = "crm_sessao";

    /// <summary>Identity com os perfis, a política de senha e de bloqueio e o cookie da seção 9.</summary>
    public static IServiceCollection AddAcesso(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<AdminInicialOptions>().BindConfiguration(AdminInicialOptions.Secao);
        services.AddScoped<InicializacaoAcesso>();

        services
            .AddIdentityCore<Usuario>(opcoes =>
            {
                // O e-mail é validado à parte; qualquer caractere que ele aceite serve como nome de usuário.
                opcoes.User.AllowedUserNameCharacters = string.Empty;
                opcoes.User.RequireUniqueEmail = true;

                opcoes.Password.RequiredLength = 10;
                opcoes.Password.RequiredUniqueChars = 1;
                opcoes.Password.RequireDigit = false;
                opcoes.Password.RequireLowercase = false;
                opcoes.Password.RequireUppercase = false;
                opcoes.Password.RequireNonAlphanumeric = false;

                opcoes.Lockout.AllowedForNewUsers = true;
                opcoes.Lockout.MaxFailedAccessAttempts = 5;
                opcoes.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CrmDbContext>()
            .AddSignInManager()
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

        services.AddAuthorization();

        return services;
    }

    public static async Task InicializarAcessoAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<InicializacaoAcesso>().ExecutarAsync();
    }
}
