using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Funil;

public sealed record EtapaResponse(int Id, string Nome, int Ordem, TipoEtapa Tipo, bool Ativa);

/// <summary>Etapas do funil. A leitura é liberada para a atendente, que usa as etapas nos filtros.</summary>
[ApiController]
[Route("api/etapas")]
[Authorize]
public sealed class EtapasController(CrmDbContext db) : ControllerBase
{
    /// <summary>Todas as etapas, ativas ou não, na ordem do funil.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EtapaResponse>>> Listar() =>
        await db.Etapas
            .OrderBy(e => e.Ordem)
            .ThenBy(e => e.Id)
            .Select(e => new EtapaResponse(e.Id, e.Nome, e.Ordem, e.Tipo, e.Ativa))
            .ToListAsync(HttpContext.RequestAborted);
}
