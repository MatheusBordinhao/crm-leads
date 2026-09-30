using Crm.Api.Infra.Dados;

namespace Crm.Api.Features.Usuarios;

internal static class ConsultasUsuarios
{
    /// <summary>Os usuários com o perfil Admin, ativos ou não.</summary>
    public static IQueryable<Usuario> Admins(this CrmDbContext db) =>
        db.Users.Where(u => db.UserRoles.Any(up =>
            up.UserId == u.Id && db.Roles.Any(p => p.Id == up.RoleId && p.Name == nameof(Perfil.Admin))));

    /// <summary>
    /// Quem participa do rodízio (seção 3.3), na ordem da vez: primeiro quem nunca recebeu lead pelo rodízio,
    /// depois quem recebeu há mais tempo; o empate é decidido pelo id.
    /// </summary>
    public static IQueryable<Usuario> NaOrdemDaVez(this IQueryable<Usuario> usuarios) =>
        usuarios
            .Where(u => u.Ativo && u.ParticipaRodizio)
            .OrderBy(u => u.UltimoLeadAtribuidoEm != null)
            .ThenBy(u => u.UltimoLeadAtribuidoEm)
            .ThenBy(u => u.Id);
}
