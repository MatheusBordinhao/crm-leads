using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Configuracao;

/// <param name="ExigirDocumentoParaGanho">Marcar Ganho exige CPF/CNPJ válido (seção 3.4).</param>
/// <param name="DistribuicaoAutomatica">
/// Os leads novos vão para o rodízio (seção 3.3). Desligada, eles ficam sem responsável e os admins são avisados.
/// </param>
public sealed record ConfiguracoesResponse(bool ExigirDocumentoParaGanho, bool DistribuicaoAutomatica);

/// <summary>Todas as configurações vão juntas: um campo ausente responde 400, em vez de voltar ao padrão.</summary>
public sealed record EditarConfiguracoesRequest(bool ExigirDocumentoParaGanho, bool DistribuicaoAutomatica)
{
    public override string ToString() =>
        $"{nameof(EditarConfiguracoesRequest)} {{ ExigirDocumentoParaGanho = {ExigirDocumentoParaGanho}, DistribuicaoAutomatica = {DistribuicaoAutomatica} }}";
}

/// <summary>Configurações do sistema, só para admin. Cada mudança entra na auditoria.</summary>
[ApiController]
[Route("api/configuracoes")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ConfiguracoesController(CrmDbContext db, Configuracoes configuracoes, AuditoriaService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConfiguracoesResponse>> Obter() => await LerAsync(HttpContext.RequestAborted);

    [HttpPut]
    public async Task<ActionResult<ConfiguracoesResponse>> Editar(EditarConfiguracoesRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        var atuais = await LerAsync(ct);
        await GravarSeMudouAsync(
            ChavesConfiguracao.ExigirDocumentoParaGanho, atuais.ExigirDocumentoParaGanho, requisicao.ExigirDocumentoParaGanho, ct);
        await GravarSeMudouAsync(
            ChavesConfiguracao.DistribuicaoAutomatica, atuais.DistribuicaoAutomatica, requisicao.DistribuicaoAutomatica, ct);
        await db.SaveChangesAsync(ct);
        return await LerAsync(ct);
    }

    private async Task<ConfiguracoesResponse> LerAsync(CancellationToken ct) => new(
        await configuracoes.ExigirDocumentoParaGanhoAsync(ct),
        await configuracoes.DistribuicaoAutomaticaAsync(ct));

    /// <summary>Grava o valor novo e o registro na auditoria, só se ele mudou.</summary>
    private async Task GravarSeMudouAsync(string chave, bool atual, bool novo, CancellationToken ct)
    {
        if (atual == novo)
        {
            return;
        }

        var valor = novo ? "true" : "false";
        if (await db.Configuracoes.SingleOrDefaultAsync(c => c.Chave == chave, ct) is { } existente)
        {
            existente.Valor = valor;
        }
        else
        {
            db.Configuracoes.Add(new ConfiguracaoSistema { Chave = chave, Valor = valor });
        }

        auditoria.Registrar(AcoesAuditoria.ConfiguracaoAlterada, EntidadesAuditoria.Configuracao, chave, new { valor = novo });
    }
}
