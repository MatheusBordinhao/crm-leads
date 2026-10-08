using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Painel;

/// <summary>
/// Painel (seção 8). Parte de <see cref="VisibilidadeLeads"/>, como a lista e o quadro: a atendente vê só os
/// próprios números, e o filtro de responsável e a quebra por atendente são do admin.
/// </summary>
[ApiController]
[Route("api/painel")]
[Authorize]
public sealed class PainelController(CrmDbContext db, VisibilidadeLeads visibilidade, TimeProvider relogio) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PainelResponse>> Obter([FromQuery] FiltroPainel filtro)
    {
        var ct = HttpContext.RequestAborted;
        var ehAdmin = visibilidade.EhAdmin;
        var periodo = PeriodoPainel.Resolver(filtro.De, filtro.Ate, HorarioBrasilia.Dia(relogio.GetUtcNow()));

        // Os leads de teste ficam de fora de todos os números; os anonimizados continuam contando.
        var reais = visibilidade.Visiveis().Where(l => !l.Teste);
        var leads = FiltrosLeads.Aplicar(reais, filtro, ehAdmin);

        // Os leads novos do período são poucos (cerca de 300 por mês): vêm numa consulta só, e as contas, como a
        // mediana, são feitas aqui.
        var coorte = await ConsultasPainel.Coorte(leads, periodo).ToListAsync(ct);
        var resumo = MetricasPainel.Resumir(coorte);
        var entradas = await ContarEntradasAsync(FiltrosLeads.Aplicar(reais, new SoResponsavel(filtro), ehAdmin), filtro, periodo, ct);

        return new PainelResponse(
            periodo.De,
            periodo.Ate,
            resumo.LeadsNovos,
            entradas,
            resumo.Ganhos,
            resumo.TaxaConversao,
            new PrimeiroContatoPainelResponse(resumo.MedianaPrimeiroContatoMinutos, resumo.ComContato, resumo.SemContato),
            await SituacaoAtualAsync(coorte, ct),
            PorOrigem(coorte),
            await PorCampanhaAsync(coorte, ct),
            await PerdasAsync(leads, periodo, ct),
            ehAdmin ? await PorAtendenteAsync(coorte, ct) : null);
    }

    /// <summary>
    /// Entradas que o CRM processou no período, incluindo as de pessoas que já eram leads. A origem e a campanha são
    /// as de cada entrada, e a visibilidade e a responsável, as do lead.
    /// </summary>
    private async Task<int> ContarEntradasAsync(IQueryable<Lead> donos, FiltroPainel filtro, PeriodoPainel periodo, CancellationToken ct)
    {
        var entradas = ConsultasPainel.Entradas(db, donos, periodo);
        if (filtro.Origem is { } origem)
        {
            entradas = entradas.Where(e => e.Origem == origem);
        }

        if (filtro.CampanhaId is { } campanhaId)
        {
            entradas = entradas.Where(e => e.CampanhaId == campanhaId);
        }

        return await entradas.CountAsync(ct);
    }

    /// <summary>
    /// Os leads novos pela etapa em que estão hoje: as etapas ativas, na ordem do funil, mesmo as vazias. Etapa com
    /// leads não é desativada; se uma for, os leads dela aparecem do mesmo jeito.
    /// </summary>
    private async Task<List<EtapaPainelResponse>> SituacaoAtualAsync(List<LeadDaCoorte> coorte, CancellationToken ct)
    {
        var porEtapa = coorte.CountBy(l => l.EtapaId).ToDictionary();
        var etapas = await db.Etapas.AsNoTracking().OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync(ct);
        return [.. etapas
            .Where(e => e.Ativa || porEtapa.ContainsKey(e.Id))
            .Select(e => new EtapaPainelResponse(new EtapaResumo(e.Id, e.Nome, e.Tipo), porEtapa.GetValueOrDefault(e.Id)))];
    }

    private static List<OrigemPainelResponse> PorOrigem(List<LeadDaCoorte> coorte) =>
        [.. coorte
            .GroupBy(l => l.Origem)
            .Select(g => (Origem: g.Key, Resumo: MetricasPainel.Resumir([.. g])))
            .OrderByDescending(g => g.Resumo.LeadsNovos)
            .ThenBy(g => g.Origem)
            .Select(g => new OrigemPainelResponse(g.Origem, g.Resumo.LeadsNovos, g.Resumo.Ganhos, g.Resumo.TaxaConversao))];

    /// <summary>
    /// Os leads novos pela campanha da primeira entrada. Os sem campanha, que não vieram de anúncio, ficam sempre por
    /// último, mesmo quando são a maioria: não são uma campanha que vende mais.
    /// </summary>
    private async Task<List<CampanhaPainelResponse>> PorCampanhaAsync(List<LeadDaCoorte> coorte, CancellationToken ct)
    {
        var ids = coorte.Where(l => l.CampanhaId is not null).Select(l => l.CampanhaId!.Value).Distinct().ToList();
        var campanhas = await db.Campanhas
            .Where(c => ids.Contains(c.Id))
            .Select(c => new CampanhaResumo(c.Id, c.Nome))
            .ToDictionaryAsync(c => c.Id, ct);
        return [.. coorte
            .GroupBy(l => l.CampanhaId)
            .Select(g => (Campanha: g.Key is { } id ? campanhas[id] : null, Resumo: MetricasPainel.Resumir([.. g])))
            .OrderBy(g => g.Campanha is null)
            .ThenByDescending(g => g.Resumo.LeadsNovos)
            .ThenBy(g => g.Campanha?.Nome, MetricasPainel.OrdemDosNomes)
            .Select(g => new CampanhaPainelResponse(g.Campanha, g.Resumo.LeadsNovos, g.Resumo.Ganhos, g.Resumo.TaxaConversao))];
    }

    /// <summary>
    /// Idas para Perdido no período, pelo histórico de etapas: de leads de qualquer data de entrada, mesmo os que
    /// depois foram reabertos, com os filtros valendo pelo lead. Cada lead conta uma vez por motivo.
    /// </summary>
    private async Task<PerdasPainelResponse> PerdasAsync(IQueryable<Lead> leads, PeriodoPainel periodo, CancellationToken ct)
    {
        var inicio = periodo.Inicio;
        var fim = periodo.Fim;
        var idas = db.HistoricoEtapas
            .Where(h => h.OcorridoEm >= inicio && h.OcorridoEm < fim)
            .Where(h => db.Etapas.Any(e => e.Id == h.EtapaNovaId && e.Tipo == TipoEtapa.Perdido))
            .Where(h => leads.Any(l => l.Id == h.LeadId));
        var porMotivo = await idas
            .GroupBy(h => h.MotivoPerdaId)
            .Select(g => new { MotivoId = g.Key, Leads = g.Select(h => h.LeadId).Distinct().Count() })
            .ToListAsync(ct);
        var leadsPerdidos = await idas.Select(h => h.LeadId).Distinct().CountAsync(ct);

        var ids = porMotivo.Where(m => m.MotivoId is not null).Select(m => m.MotivoId!.Value).ToList();
        var motivos = await db.MotivosPerda
            .Where(m => ids.Contains(m.Id))
            .Select(m => new MotivoPerdaResumo(m.Id, m.Nome))
            .ToDictionaryAsync(m => m.Id, ct);
        return new PerdasPainelResponse(
            leadsPerdidos,
            [.. porMotivo
                .Select(m => new MotivoPerdaPainelResponse(m.MotivoId is { } id ? motivos[id] : null, m.Leads))
                .OrderByDescending(m => m.Leads)
                .ThenBy(m => m.Motivo is null)
                .ThenBy(m => m.Motivo?.Nome, MetricasPainel.OrdemDosNomes)]);
    }

    /// <summary>
    /// Os leads novos pela responsável atual: quem foi desativada continua com os números dela, e os leads sem
    /// responsável ficam numa linha própria, por último no empate.
    /// </summary>
    private async Task<List<AtendentePainelResponse>> PorAtendenteAsync(List<LeadDaCoorte> coorte, CancellationToken ct)
    {
        var ids = coorte.Where(l => l.ResponsavelId is not null).Select(l => l.ResponsavelId!.Value).Distinct().ToList();
        var responsaveis = await db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new ResponsavelPainelResponse(u.Id, u.Nome, u.Ativo))
            .ToDictionaryAsync(r => r.Id, ct);
        return [.. coorte
            .GroupBy(l => l.ResponsavelId)
            .Select(g => (Responsavel: g.Key is { } id ? responsaveis[id] : null, Resumo: MetricasPainel.Resumir([.. g])))
            .OrderByDescending(g => g.Resumo.LeadsNovos)
            .ThenBy(g => g.Responsavel is null)
            .ThenBy(g => g.Responsavel?.Nome, MetricasPainel.OrdemDosNomes)
            .Select(g => new AtendentePainelResponse(
                g.Responsavel, g.Resumo.LeadsNovos, g.Resumo.Ganhos, g.Resumo.TaxaConversao, g.Resumo.MedianaPrimeiroContatoMinutos))];
    }

    /// <summary>Só o filtro de responsável, para as entradas, que têm a origem e a campanha de cada uma.</summary>
    private sealed class SoResponsavel(IFiltroLeads filtro) : IFiltroLeads
    {
        public string? Busca => null;

        public Origem? Origem => null;

        public int? CampanhaId => null;

        public Guid? ResponsavelId => filtro.ResponsavelId;

        public bool SemResponsavel => filtro.SemResponsavel;
    }
}
