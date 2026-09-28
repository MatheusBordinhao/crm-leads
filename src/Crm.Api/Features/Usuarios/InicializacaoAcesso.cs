using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Crm.Api.Features.Usuarios;

/// <summary>Primeiro admin, lido de <c>Admin__Email</c> e <c>Admin__SenhaInicial</c>.</summary>
internal sealed class AdminInicialOptions
{
    public const string Secao = "Admin";

    public string? Email { get; set; }

    public string? SenhaInicial { get; set; }
}

/// <summary>
/// Roda na subida da API, em todos os ambientes: cria os perfis que faltarem e, se ainda não houver nenhum
/// admin, cria o primeiro com a senha do <c>.env</c>, que ele precisa trocar no primeiro acesso.
/// </summary>
internal sealed class InicializacaoAcesso(
    CrmDbContext db,
    UserManager<Usuario> usuarios,
    RoleManager<IdentityRole<Guid>> perfis,
    IOptions<AdminInicialOptions> adminInicial,
    TimeProvider relogio,
    ILogger<InicializacaoAcesso> logger)
{
    public const string NomeAdminInicial = "Administrador";

    public async Task ExecutarAsync()
    {
        foreach (var perfil in Enum.GetNames<Perfil>())
        {
            if (!await perfis.RoleExistsAsync(perfil))
            {
                Garantir(await perfis.CreateAsync(new IdentityRole<Guid>(perfil)), $"criar o perfil {perfil}");
            }
        }

        if ((await usuarios.GetUsersInRoleAsync(nameof(Perfil.Admin))).Count > 0)
        {
            return;
        }

        var email = adminInicial.Value.Email?.Trim().ToLowerInvariant();
        var senha = adminInicial.Value.SenhaInicial;
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(senha))
        {
            throw new InvalidOperationException(
                "Nenhum admin cadastrado. Configure Admin__Email e Admin__SenhaInicial no .env (veja o README).");
        }

        var admin = new Usuario
        {
            UserName = email,
            Email = email,
            Nome = NomeAdminInicial,
            TrocarSenha = true,
            CriadoEm = relogio.GetUtcNow(),
        };

        await using var transacao = await db.Database.BeginTransactionAsync();
        Garantir(await usuarios.CreateAsync(admin, senha), "criar o primeiro admin");
        Garantir(await usuarios.AddToRoleAsync(admin, nameof(Perfil.Admin)), "dar o perfil Admin ao primeiro admin");
        await transacao.CommitAsync();

        logger.LogInformation("Primeiro admin criado: {UsuarioId}", admin.Id);
    }

    private static void Garantir(IdentityResult resultado, string acao)
    {
        if (!resultado.Succeeded)
        {
            var erros = string.Join(" ", resultado.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Não foi possível {acao}: {erros}");
        }
    }
}
