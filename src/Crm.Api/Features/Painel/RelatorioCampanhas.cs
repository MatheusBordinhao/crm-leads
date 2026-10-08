using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Painel;

/// <summary>Quantas entradas do período vieram de uma origem com uma campanha, ou sem campanha.</summary>
public sealed record EntradasDoGrupo(Origem Origem, int? CampanhaId, int Quantidade);

/// <summary>Uma campanha do relatório, com a origem, que vai na coluna Origem da linha dela.</summary>
public sealed record CampanhaDoRelatorio(int Id, Origem Origem, string Nome);

/// <summary>
/// Relatório por campanha (seção 8), com as consultas e as definições do painel. Só o admin e a integração chegam
/// aqui, e o relatório conta todos os leads reais: ele não tem dados pessoais e não usa a <see cref="VisibilidadeLeads"/>,
/// que não enxerga o n8n.
/// </summary>
public sealed class RelatorioCampanhas(CrmDbContext db, TimeProvider relogio)
{
    /// <summary>O relatório do período; sem datas, os últimos 30 dias, com hoje, como no painel.</summary>
    public Task<RelatorioCampanhasResponse> GerarAsync(IFiltroPeriodo filtro, CancellationToken ct) =>
        GerarAsync(PeriodoPainel.Resolver(filtro.De, filtro.Ate, HorarioBrasilia.Dia(relogio.GetUtcNow())), ct);

    private async Task<RelatorioCampanhasResponse> GerarAsync(PeriodoPainel periodo, CancellationToken ct)
    {
        // Os leads de teste ficam de fora; os anonimizados continuam contando.
        var reais = db.Leads.Where(l => !l.Teste);
        var coorte = await ConsultasPainel.Coorte(reais, periodo).ToListAsync(ct);
        var entradas = (await ConsultasPainel.Entradas(db, reais, periodo)
            .GroupBy(e => new { e.Origem, e.CampanhaId })
            .Select(g => new { g.Key.Origem, g.Key.CampanhaId, Quantidade = g.Count() })
            .ToListAsync(ct))
            .Select(g => new EntradasDoGrupo(g.Origem, g.CampanhaId, g.Quantidade))
            .ToList();

        var ids = coorte.Select(l => l.CampanhaId).Concat(entradas.Select(e => e.CampanhaId)).OfType<int>().Distinct().ToList();
        var campanhas = await db.Campanhas
            .Where(c => ids.Contains(c.Id))
            .Select(c => new CampanhaDoRelatorio(c.Id, c.Origem, c.Nome))
            .ToListAsync(ct);
        return MontagemRelatorio.Montar(periodo, coorte, entradas, campanhas);
    }
}

/// <summary>
/// A montagem do relatório por campanha, sem banco. Os leads novos e as entradas do período são agrupados pela campanha
/// ou, sem campanha, pela origem. Cada linha usa o <see cref="MetricasPainel.Resumir"/>, como o painel, e o total é o
/// resumo de todos os leads novos, com todas as entradas: os números do painel sem filtros.
/// </summary>
internal static class MontagemRelatorio
{
    public static RelatorioCampanhasResponse Montar(
        PeriodoPainel periodo,
        IReadOnlyCollection<LeadDaCoorte> coorte,
        IReadOnlyCollection<EntradasDoGrupo> entradas,
        IReadOnlyCollection<CampanhaDoRelatorio> campanhas)
    {
        var porId = campanhas.ToDictionary(c => c.Id);

        // A campanha já tem a origem, e uma campanha fica numa linha só; sem campanha, a linha é a da origem.
        Chave ChaveDe(Origem origem, int? campanhaId) =>
            campanhaId is { } id ? new Chave(porId[id].Origem, id) : new Chave(origem, null);

        var leads = coorte.ToLookup(l => ChaveDe(l.Origem, l.CampanhaId));
        var entradasPorChave = entradas
            .GroupBy(e => ChaveDe(e.Origem, e.CampanhaId))
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Quantidade));

        // Os sem campanha não vieram de anúncio: ficam sempre no fim, mesmo quando são a maioria, como no painel.
        var linhas = leads.Select(g => g.Key)
            .Union(entradasPorChave.Keys)
            .Select(chave => Linha(chave, porId, [.. leads[chave]], entradasPorChave.GetValueOrDefault(chave)))
            .OrderBy(l => l.Campanha is null)
            .ThenByDescending(l => l.LeadsNovos)
            .ThenBy(l => l.Campanha?.Nome, MetricasPainel.OrdemDosNomes)
            .ThenBy(l => l.Origem)
            .ThenBy(l => l.Campanha?.Id)
            .ToList();

        var total = MetricasPainel.Resumir(coorte);
        return new RelatorioCampanhasResponse(
            periodo.De,
            periodo.Ate,
            linhas,
            new TotalRelatorioResponse(
                total.LeadsNovos,
                entradas.Sum(e => e.Quantidade),
                total.EmAndamento,
                total.Ganhos,
                total.Perdidos,
                total.TaxaConversao,
                total.MedianaPrimeiroContatoMinutos));
    }

    private static LinhaRelatorioResponse Linha(
        Chave chave, Dictionary<int, CampanhaDoRelatorio> campanhas, List<LeadDaCoorte> leads, int entradas)
    {
        var resumo = MetricasPainel.Resumir(leads);
        return new LinhaRelatorioResponse(
            chave.Origem,
            chave.CampanhaId is { } id ? new CampanhaResumo(id, campanhas[id].Nome) : null,
            resumo.LeadsNovos,
            entradas,
            resumo.EmAndamento,
            resumo.Ganhos,
            resumo.Perdidos,
            resumo.TaxaConversao,
            resumo.MedianaPrimeiroContatoMinutos);
    }

    /// <summary>A linha: a campanha, com a origem dela, ou a origem dos leads e das entradas sem campanha.</summary>
    private readonly record struct Chave(Origem Origem, int? CampanhaId);
}

internal static class PainelExtensions
{
    /// <summary>O relatório por campanha, da tela do admin e da integração.</summary>
    public static IServiceCollection AddPainel(this IServiceCollection services) => services.AddScoped<RelatorioCampanhas>();
}
