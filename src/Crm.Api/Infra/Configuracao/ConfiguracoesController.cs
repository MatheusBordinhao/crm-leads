using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Configuracao;

/// <param name="ExigirDocumentoParaGanho">Marcar Ganho exige CPF/CNPJ válido (seção 3.4).</param>
public sealed record ConfiguracoesResponse(bool ExigirDocumentoParaGanho);

public sealed record EditarConfiguracoesRequest(bool ExigirDocumentoParaGanho)
{
    public override string ToString() =>
        $"{nameof(EditarConfiguracoesRequest)} {{ ExigirDocumentoParaGanho = {ExigirDocumentoParaGanho} }}";
}

/// <summary>Configurações do sistema, só para admin. Cada mudança entra na auditoria.</summary>
[ApiController]
[Route("api/configuracoes")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ConfiguracoesController(CrmDbContext db, Configuracoes configuracoes, AuditoriaService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConfiguracoesResponse>> Obter() =>
        new ConfiguracoesResponse(await configuracoes.ExigirDocumentoParaGanhoAsync(HttpContext.RequestAborted));

    [HttpPut]
    public async Task<ActionResult<ConfiguracoesResponse>> Editar(EditarConfiguracoesRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        if (requisicao.ExigirDocumentoParaGanho != await configuracoes.ExigirDocumentoParaGanhoAsync(ct))
        {
            var valor = requisicao.ExigirDocumentoParaGanho ? "true" : "false";
            if (await db.Configuracoes.SingleOrDefaultAsync(c => c.Chave == ChavesConfiguracao.ExigirDocumentoParaGanho, ct) is { } existente)
            {
                existente.Valor = valor;
            }
            else
            {
                db.Configuracoes.Add(new ConfiguracaoSistema { Chave = ChavesConfiguracao.ExigirDocumentoParaGanho, Valor = valor });
            }

            auditoria.Registrar(
                AcoesAuditoria.ConfiguracaoAlterada,
                EntidadesAuditoria.Configuracao,
                ChavesConfiguracao.ExigirDocumentoParaGanho,
                new { valor = requisicao.ExigirDocumentoParaGanho });
            await db.SaveChangesAsync(ct);
        }

        return await Obter();
    }
}
