using Crm.Api.Features.Leads;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Crm.Api.Features.Integracoes;

/// <summary>Rotas do n8n, com a chave de integração no cabeçalho <c>X-Api-Key</c>. O cookie de usuário não vale aqui.</summary>
[ApiController]
[Route("api/integracoes")]
[Authorize(Policy = Politicas.Integracao)]
[EnableRateLimiting(IntegracaoExtensions.PoliticaLimite)]
public sealed class IntegracoesController(ReceberEntrada receberEntrada, RelatorioCampanhas relatorio) : ControllerBase
{
    private const int TamanhoMaximoCorpo = 64 * 1024;

    /// <summary>
    /// Recebe um lead da Meta, do Google ou do site. É idempotente por origem e idExterno: um reenvio responde
    /// <c>ja_recebido</c> e não duplica o lead.
    /// </summary>
    [HttpPost("leads")]
    [GuardarCorpo]
    [RequestSizeLimit(TamanhoMaximoCorpo)]
    [ProducesResponseType<EntradaIntegracaoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<EntradaIntegracaoResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EntradaIntegracaoResponse>> ReceberLead(EntradaIntegracaoRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        var processada = await receberEntrada.ExecutarAsync(
            new NovaEntrada
            {
                Origem = EnumTexto<Origem>.Ler(requisicao.Origem!),
                IdExterno = requisicao.IdExterno,
                RecebidoEm = requisicao.RecebidoEm!.Value,
                Teste = requisicao.Teste ?? false,
                Nome = requisicao.Nome!,
                Email = requisicao.Email,
                Telefone = requisicao.Telefone,
                CpfCnpj = requisicao.CpfCnpj,
                Uf = requisicao.Uf,
                Empresa = requisicao.Empresa,
                Plataforma = requisicao.Plataforma,
                Campanha = requisicao.Campanha,
                Conjunto = requisicao.Conjunto,
                Anuncio = requisicao.Anuncio,
                Formulario = requisicao.Formulario,
                Utm = requisicao.Utm,
                Gclid = requisicao.Gclid,
                RespostasExtras = requisicao.RespostasExtras,
                PayloadBruto = await Request.LerCorpoGuardadoAsync(ct),
            },
            ct);

        var resposta = new EntradaIntegracaoResponse(processada.LeadId, processada.Resultado);
        return processada.Resultado == ResultadoEntrada.Criado ? StatusCode(StatusCodes.Status201Created, resposta) : resposta;
    }

    /// <summary>
    /// Relatório por campanha (seção 4.4), o mesmo da tela do admin, sem dados pessoais: uma linha por campanha, as dos
    /// leads sem campanha no fim, e o total. Sem datas, a semana anterior inteira, de segunda a domingo no horário de
    /// Brasília: o fluxo semanal da Gama só chama a rota.
    /// </summary>
    [HttpGet("relatorios/campanhas")]
    public async Task<ActionResult<RelatorioCampanhasResponse>> RelatorioPorCampanha([FromQuery] FiltroRelatorio filtro) =>
        await relatorio.GerarAsync(filtro, PeriodoPadrao.SemanaAnterior, HttpContext.RequestAborted);

    /// <summary>O mesmo relatório no CSV que o admin baixa na tela, para o e-mail da Gama. Sem datas, a semana anterior.</summary>
    [HttpGet("relatorios/campanhas/csv")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> RelatorioPorCampanhaCsv([FromQuery] FiltroRelatorio filtro) =>
        RelatorioCampanhasCsv.Arquivo(await relatorio.GerarAsync(filtro, PeriodoPadrao.SemanaAnterior, HttpContext.RequestAborted));
}
