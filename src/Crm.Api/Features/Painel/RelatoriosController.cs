using Crm.Api.Features.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Painel;

/// <summary>
/// Relatório por campanha (seção 8), só para o admin: a atendente recebe 403. O n8n busca o mesmo relatório, com a chave
/// de integração, em <c>/api/integracoes/relatorios/campanhas</c>.
/// </summary>
[ApiController]
[Route("api/relatorios")]
[Authorize(Policy = Politicas.Admin)]
public sealed class RelatoriosController(RelatorioCampanhas relatorio) : ControllerBase
{
    /// <summary>
    /// Os números de cada campanha no período, sem dados pessoais. Sem datas, os últimos 30 dias, com hoje, como no
    /// painel.
    /// </summary>
    [HttpGet("campanhas")]
    public async Task<ActionResult<RelatorioCampanhasResponse>> Campanhas([FromQuery] FiltroRelatorio filtro) =>
        await relatorio.GerarAsync(filtro, PeriodoPadrao.UltimosTrintaDias, HttpContext.RequestAborted);

    /// <summary>O mesmo relatório em CSV para o Excel (UTF-8 com BOM e ponto e vírgula), com o total na última linha.</summary>
    [HttpGet("campanhas/csv")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> CampanhasCsv([FromQuery] FiltroRelatorio filtro) =>
        RelatorioCampanhasCsv.Arquivo(await relatorio.GerarAsync(filtro, PeriodoPadrao.UltimosTrintaDias, HttpContext.RequestAborted));
}
