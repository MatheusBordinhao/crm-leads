using System.Globalization;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <param name="NomeProvisorio">O nome foi gerado porque a campanha chegou sem nome; o admin pode renomear.</param>
public sealed record CampanhaResponse(int Id, Origem Origem, string IdExterno, string Nome, bool NomeProvisorio);

/// <param name="Nome">O nome dado pelo admin, que deixa de ser provisório.</param>
public sealed record RenomearCampanhaRequest(string Nome) : ICadastroComNome
{
    public override string ToString() => nameof(RenomearCampanhaRequest);
}

internal sealed class RenomearCampanhaRequestValidator : Validador<RenomearCampanhaRequest>
{
    public RenomearCampanhaRequestValidator() => RegrasNome.Aplicar(this, Campanha.TamanhoMaximoNome);
}

/// <summary>
/// Campanhas. A leitura é liberada para a atendente, que usa as campanhas nos filtros; renomear é do admin, em
/// Configurações › Campanhas (Passo 6).
/// </summary>
[ApiController]
[Route("api/campanhas")]
[Authorize]
public sealed class CampanhasController(CrmDbContext db, AuditoriaService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampanhaResponse>>> Listar() =>
        await db.Campanhas
            .OrderBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .Select(c => new CampanhaResponse(c.Id, c.Origem, c.IdExterno, c.Nome, c.NomeProvisorio))
            .ToListAsync(HttpContext.RequestAborted);

    /// <summary>
    /// Renomeia a campanha, como a "Google Ads · campanha 123" que chegou só com o id. O nome dado pelo admin deixa de
    /// ser provisório, e um nome que chegue depois pela integração ou pela importação não o substitui (seção 3.2).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<CampanhaResponse>> Renomear(int id, RenomearCampanhaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        var chave = await db.Campanhas
            .Where(c => c.Id == id)
            .Select(c => new { c.Origem, c.IdExterno })
            .SingleOrDefaultAsync(ct);
        if (chave is null)
        {
            return NotFound();
        }

        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        // A mesma trava do ReceberEntrada: uma entrada que traga o nome da campanha ao mesmo tempo não sobrescreve o do
        // admin, porque espera a renomeação terminar e então já vê o nome definitivo.
        var trava = $"campanha:{EnumTexto<Origem>.Nome(chave.Origem)}:{chave.IdExterno}";
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({trava}, 0))", ct);
        var campanha = await db.Campanhas.SingleAsync(c => c.Id == id, ct);

        var nome = Textos.Limpar(requisicao.Nome)!;
        if (nome != campanha.Nome || campanha.NomeProvisorio)
        {
            campanha.Nome = nome;
            campanha.NomeProvisorio = false;
            auditoria.Registrar(
                AcoesAuditoria.CampanhaRenomeada,
                EntidadesAuditoria.Campanha,
                campanha.Id.ToString(CultureInfo.InvariantCulture),
                new { campos = new[] { "nome" } });
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }

        return new CampanhaResponse(campanha.Id, campanha.Origem, campanha.IdExterno, campanha.Nome, campanha.NomeProvisorio);
    }
}
