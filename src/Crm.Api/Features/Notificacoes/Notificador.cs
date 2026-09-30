using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Notificacoes;

/// <summary>
/// Grava notificações no contexto: elas entram no próximo <c>SaveChanges</c>, na mesma transação do que
/// aconteceu. Quem fez a ação (o autor) nunca é notificado dela.
/// </summary>
public sealed class Notificador(CrmDbContext db, TimeProvider relogio)
{
    /// <summary>Para uma pessoa. Não faz nada sem destinatário ou quando o destinatário é o autor da ação.</summary>
    public void ParaUsuario(Guid? usuarioId, TipoNotificacao tipo, string titulo, long? leadId, Guid? autorId)
    {
        if (usuarioId is { } destinatario && destinatario != autorId)
        {
            db.Notificacoes.Add(Nova(destinatario, tipo, titulo, leadId));
        }
    }

    /// <summary>Para cada admin ativo, menos o autor da ação.</summary>
    public async Task ParaAdminsAsync(TipoNotificacao tipo, string titulo, long? leadId, Guid? autorId, CancellationToken ct)
    {
        var admins = await db.Admins().Where(u => u.Ativo && u.Id != autorId).Select(u => u.Id).ToListAsync(ct);
        db.Notificacoes.AddRange(admins.Select(admin => Nova(admin, tipo, titulo, leadId)));
    }

    private Notificacao Nova(Guid usuarioId, TipoNotificacao tipo, string titulo, long? leadId) => new()
    {
        UsuarioId = usuarioId,
        Tipo = tipo,
        Titulo = titulo.Length > Notificacao.TamanhoMaximoTitulo ? titulo[..Notificacao.TamanhoMaximoTitulo] : titulo,
        LeadId = leadId,
        CriadoEm = relogio.GetUtcNow(),
    };
}

internal static class NotificacoesExtensions
{
    public static IServiceCollection AddNotificacoes(this IServiceCollection services) =>
        services.AddScoped<Notificador>().AddScoped<AvisosLeads>();
}
