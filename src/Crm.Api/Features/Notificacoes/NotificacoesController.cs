using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Notificacoes;

/// <summary>
/// Notificações do usuário logado (seção 3.6). Como o resto da API, respeita <see cref="VisibilidadeLeads"/>: a
/// notificação de um lead que a pessoa não vê mais (porque ele passou para outra atendente) some da lista e
/// responde 404.
/// </summary>
[ApiController]
[Route("api/notificacoes")]
[Authorize]
public sealed class NotificacoesController(CrmDbContext db, VisibilidadeLeads visibilidade, TimeProvider relogio) : ControllerBase
{
    public const int Quantidade = 30;

    /// <summary>As 30 notificações mais recentes e quantas ainda não foram lidas.</summary>
    [HttpGet]
    public async Task<ActionResult<NotificacoesResponse>> Listar()
    {
        var ct = HttpContext.RequestAborted;
        var minhas = Minhas();
        var leads = visibilidade.Visiveis();
        var naoLidas = await minhas.CountAsync(n => n.LidaEm == null, ct);
        var itens = await minhas
            .OrderByDescending(n => n.CriadoEm)
            .ThenByDescending(n => n.Id)
            .Take(Quantidade)
            .Select(n => new NotificacaoResponse(
                n.Id,
                n.Tipo,
                n.Titulo,
                leads.Where(l => l.Id == n.LeadId).Select(l => new LeadDaNotificacao(l.Id, l.Nome)).FirstOrDefault(),
                n.CriadoEm,
                n.LidaEm))
            .ToListAsync(ct);

        return new NotificacoesResponse(naoLidas, itens);
    }

    [HttpPost("{id:long}/lida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarLida(long id)
    {
        var ct = HttpContext.RequestAborted;
        var notificacao = await Minhas().SingleOrDefaultAsync(n => n.Id == id, ct);
        if (notificacao is null)
        {
            return NotFound();
        }

        notificacao.LidaEm ??= relogio.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("lidas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarcarTodasLidas()
    {
        var agora = relogio.GetUtcNow();
        await Minhas()
            .Where(n => n.LidaEm == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.LidaEm, agora), HttpContext.RequestAborted);
        return NoContent();
    }

    /// <summary>As notificações do usuário logado que não são de lead ou são de um lead que ele ainda vê.</summary>
    private IQueryable<Notificacao> Minhas()
    {
        var usuarioId = visibilidade.UsuarioId;
        var leads = visibilidade.Visiveis();
        return db.Notificacoes.Where(n => n.UsuarioId == usuarioId && (n.LeadId == null || leads.Any(l => l.Id == n.LeadId)));
    }
}
