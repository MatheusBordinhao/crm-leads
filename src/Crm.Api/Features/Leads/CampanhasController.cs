using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <param name="NomeProvisorio">O nome foi gerado porque a campanha chegou sem nome; o admin pode renomear.</param>
public sealed record CampanhaResponse(int Id, Origem Origem, string IdExterno, string Nome, bool NomeProvisorio);

/// <summary>Campanhas. A leitura é liberada para a atendente, que usa as campanhas nos filtros.</summary>
[ApiController]
[Route("api/campanhas")]
[Authorize]
public sealed class CampanhasController(CrmDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampanhaResponse>>> Listar() =>
        await db.Campanhas
            .OrderBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .Select(c => new CampanhaResponse(c.Id, c.Origem, c.IdExterno, c.Nome, c.NomeProvisorio))
            .ToListAsync(HttpContext.RequestAborted);
}
