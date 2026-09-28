using System.Security.Claims;
using System.Text.Json;
using Crm.Api.Infra.Dados;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Api.Infra.Auditoria;

internal static class AcoesAuditoria
{
    public const string LoginSucesso = "login.sucesso";
    public const string LoginFalha = "login.falha";
    public const string SenhaTrocada = "senha.trocada";
    public const string UsuarioCriado = "usuario.criado";
    public const string UsuarioEditado = "usuario.editado";
    public const string UsuarioSenhaRedefinida = "usuario.senha_redefinida";
}

internal static class EntidadesAuditoria
{
    public const string Usuario = "usuario";
}

/// <summary>
/// Registra ações na auditoria. O registro entra no contexto e é gravado no próximo <c>SaveChanges</c>,
/// na mesma transação da mudança. Os detalhes não podem ter dados pessoais: use ids e nomes de campos.
/// </summary>
public sealed class AuditoriaService(CrmDbContext db, IHttpContextAccessor http, TimeProvider relogio)
{
    /// <param name="usuarioId">Quem fez a ação. Se omitido, é o usuário logado.</param>
    public void Registrar(
        string acao, string? entidade = null, string? entidadeId = null, object? detalhes = null, Guid? usuarioId = null)
    {
        db.Auditoria.Add(new RegistroAuditoria
        {
            UsuarioId = usuarioId ?? UsuarioLogado(),
            Acao = acao,
            Entidade = entidade,
            EntidadeId = entidadeId,
            Detalhes = detalhes is null ? null : JsonSerializer.SerializeToDocument(detalhes, JsonSerializerOptions.Web),
            Ip = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            OcorridoEm = relogio.GetUtcNow(),
        });
    }

    private Guid? UsuarioLogado() =>
        Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

internal static class AuditoriaExtensions
{
    public static IServiceCollection AddAuditoria(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);
        return services.AddScoped<AuditoriaService>();
    }
}
