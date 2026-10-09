using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>LGPD (seção 3.8), só para o admin: a atendente recebe 403. Tudo entra na auditoria.</summary>
[ApiController]
[Route("api/leads/{id:long}")]
[Authorize(Policy = Politicas.Admin)]
public sealed class LgpdController(CrmDbContext db, VisibilidadeLeads visibilidade, Anonimizacao anonimizacao) : ControllerBase
{
    /// <summary>
    /// Anonimiza o lead, sem volta: tira os dados pessoais do lead, das entradas, das interações e do histórico, e apaga os
    /// eventos para o n8n. Etapa, datas, origem, campanha e responsável continuam, para o painel manter os totais.
    /// Anonimizar de novo não muda nada. Com uma importação processando, responde 409: o arquivo dela ainda guarda os dados
    /// das pessoas.
    /// </summary>
    [HttpPost("anonimizar")]
    public async Task<ActionResult<LeadResponse>> Anonimizar(long id)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Travado antes da leitura, como na mudança de etapa: uma entrada da mesma pessoa espera e depois não acha mais o lead.
        await db.TravarLeadAsync(id, ct);
        var lead = await visibilidade.Visiveis().SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null)
        {
            return NotFound();
        }

        if (!await anonimizacao.AnonimizarAsync(lead, ct))
        {
            return Problem(detail: Anonimizacao.MensagemImportacaoEmAndamento, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return (await FichaLead.BuscarAsync(visibilidade.Visiveis(), id, ct))!;
    }
}
