using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Auditoria;

/// <summary>
/// Consulta da auditoria (seção 3.8), só para o admin: a atendente recebe 403. Os registros nunca têm dados pessoais, então
/// saem como foram gravados.
/// </summary>
[ApiController]
[Route("api/auditoria")]
[Authorize(Policy = Politicas.Admin)]
public sealed class AuditoriaController(CrmDbContext db) : ControllerBase
{
    /// <summary>Os registros, do mais recente para o mais antigo, com quem fez e a paginação.</summary>
    [HttpGet]
    public async Task<ActionResult<ListaAuditoriaResponse>> Listar([FromQuery] FiltroAuditoria filtro)
    {
        var ct = HttpContext.RequestAborted;
        var consulta = Filtrar(db.Auditoria.AsNoTracking(), filtro);
        var total = await consulta.CountAsync(ct);
        var registros = await consulta
            .OrderByDescending(r => r.OcorridoEm)
            .ThenByDescending(r => r.Id)
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .Select(r => new
            {
                Registro = r,
                Usuario = db.Users.Where(u => u.Id == r.UsuarioId).Select(u => new UsuarioResumo(u.Id, u.Nome)).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var itens = registros
            .Select(r => new RegistroAuditoriaResponse(
                r.Registro.Id,
                r.Registro.OcorridoEm,
                r.Usuario,
                r.Registro.Acao,
                r.Registro.Entidade,
                r.Registro.EntidadeId,
                r.Registro.Detalhes?.RootElement.Clone(),
                r.Registro.Ip))
            .ToList();
        return new ListaAuditoriaResponse(itens, total, filtro.Pagina, filtro.Tamanho);
    }

    private static IQueryable<RegistroAuditoria> Filtrar(IQueryable<RegistroAuditoria> consulta, FiltroAuditoria filtro)
    {
        if (filtro.De is { } de)
        {
            var inicio = HorarioBrasilia.InicioDoDia(de);
            consulta = consulta.Where(r => r.OcorridoEm >= inicio);
        }

        if (filtro.Ate is { } ate)
        {
            var fim = HorarioBrasilia.InicioDoDia(ate.AddDays(1));
            consulta = consulta.Where(r => r.OcorridoEm < fim);
        }

        if (filtro.UsuarioId is { } usuarioId)
        {
            consulta = consulta.Where(r => r.UsuarioId == usuarioId);
        }

        if (Texto(filtro.Acao) is { } acao)
        {
            consulta = consulta.Where(r => r.Acao == acao);
        }

        if (Texto(filtro.Entidade) is { } entidade)
        {
            consulta = consulta.Where(r => r.Entidade == entidade);
        }

        if (Texto(filtro.EntidadeId) is { } entidadeId)
        {
            consulta = consulta.Where(r => r.EntidadeId == entidadeId);
        }

        return consulta;
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
