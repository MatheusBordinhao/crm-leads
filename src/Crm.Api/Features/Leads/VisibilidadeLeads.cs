using System.Security.Claims;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Quem vê quais leads (regra que não pode quebrar): o admin vê todos; a atendente, só os seus, e nunca os de
/// teste. Toda consulta de leads da API parte de <see cref="Visiveis"/>, e um lead fora dela responde 404.
/// </summary>
public sealed class VisibilidadeLeads(CrmDbContext db, IHttpContextAccessor http)
{
    private ClaimsPrincipal Usuario =>
        http.HttpContext?.User ?? throw new InvalidOperationException("A visibilidade de leads só vale numa requisição.");

    public bool EhAdmin => Usuario.IsInRole(nameof(Perfil.Admin));

    /// <summary>Id do usuário logado. Nulo só se a requisição não tiver sessão.</summary>
    public Guid? UsuarioId => Guid.TryParse(Usuario.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public IQueryable<Lead> Visiveis()
    {
        if (EhAdmin)
        {
            return db.Leads;
        }

        var usuarioId = UsuarioId;
        return usuarioId is null
            ? db.Leads.Where(_ => false)
            : db.Leads.Where(l => l.ResponsavelId == usuarioId && !l.Teste);
    }
}
