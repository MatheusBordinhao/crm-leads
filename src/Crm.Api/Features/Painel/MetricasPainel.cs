using System.Globalization;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Datas;

namespace Crm.Api.Features.Painel;

/// <summary>
/// Período do painel (seção 8): dias no horário de Brasília, com o primeiro e o último incluídos. Sem datas, os
/// últimos 30 dias, com hoje, como o Ganho e o Perdido do funil.
/// </summary>
public readonly record struct PeriodoPainel(DateOnly De, DateOnly Ate)
{
    public const int DiasPadrao = 30;

    /// <summary>As datas aceitas: o CRM não tem nada antes, e as extremas estourariam o <see cref="DateOnly"/>.</summary>
    public static readonly DateOnly MenorData = new(2000, 1, 1);

    public static readonly DateOnly MaiorData = new(2099, 12, 31);

    /// <summary>Instante, em UTC, em que o período começa: 00:00 do primeiro dia em Brasília.</summary>
    public DateTimeOffset Inicio => HorarioBrasilia.InicioDoDia(De);

    /// <summary>Instante, em UTC, logo depois do período: 00:00 do dia seguinte ao último, em Brasília.</summary>
    public DateTimeOffset Fim => HorarioBrasilia.InicioDoDia(Ate.AddDays(1));

    /// <summary>Sem o fim, o período vai até hoje; sem o início, são os 30 dias que terminam no fim.</summary>
    public static PeriodoPainel Resolver(DateOnly? de, DateOnly? ate, DateOnly hoje)
    {
        var fim = ate ?? hoje;
        return new PeriodoPainel(de ?? fim.AddDays(-(DiasPadrao - 1)), fim);
    }

    public static bool DataAceita(DateOnly? dia) => dia is null || (dia >= MenorData && dia <= MaiorData);
}

/// <summary>Um lead novo do período, só com o que as métricas usam.</summary>
/// <param name="TipoEtapa">O tipo da etapa em que o lead está hoje: Ganho conta na conversão.</param>
/// <param name="ResponsavelId">A responsável atual.</param>
/// <param name="Origem">A origem da primeira entrada.</param>
/// <param name="CampanhaId">A campanha da primeira entrada.</param>
public sealed record LeadDaCoorte(
    int EtapaId,
    TipoEtapa TipoEtapa,
    Guid? ResponsavelId,
    Origem Origem,
    int? CampanhaId,
    DateTimeOffset CriadoEm,
    DateTimeOffset? PrimeiroContatoEm);

/// <summary>Os números de um grupo de leads novos: o período inteiro, uma origem, uma campanha ou uma atendente.</summary>
/// <param name="Ganhos">Os leads novos que hoje estão em Ganho.</param>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1; nula sem leads novos.</param>
/// <param name="MedianaPrimeiroContatoMinutos">Nula quando nenhum lead do grupo teve contato.</param>
public sealed record ResumoCoorte(
    int LeadsNovos, int Ganhos, double? TaxaConversao, double? MedianaPrimeiroContatoMinutos, int ComContato, int SemContato);

/// <summary>
/// Cálculo das métricas da seção 8 sobre os leads novos do período, sem banco. O painel e o relatório por campanha
/// (Passo 9) usam as mesmas definições.
/// </summary>
public static class MetricasPainel
{
    /// <summary>Ordem dos nomes na tela: alfabética em português, sem diferenciar maiúsculas.</summary>
    public static readonly StringComparer OrdemDosNomes = StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    public static ResumoCoorte Resumir(IReadOnlyCollection<LeadDaCoorte> leads)
    {
        var ganhos = leads.Count(l => l.TipoEtapa == TipoEtapa.Ganho);
        var minutosAteOContato = leads
            .Where(l => l.PrimeiroContatoEm is not null)
            .Select(l => (l.PrimeiroContatoEm!.Value - l.CriadoEm).TotalMinutes)
            .ToList();
        return new ResumoCoorte(
            leads.Count,
            ganhos,
            Taxa(ganhos, leads.Count),
            Mediana(minutosAteOContato),
            minutosAteOContato.Count,
            leads.Count - minutosAteOContato.Count);
    }

    /// <summary>A parte do total, de 0 a 1. Sem total, não há taxa.</summary>
    public static double? Taxa(int parte, int total) => total == 0 ? null : (double)parte / total;

    /// <summary>O valor do meio; com uma quantidade par, a média dos dois do meio. Sem valores, não há mediana.</summary>
    public static double? Mediana(IEnumerable<double> valores)
    {
        var ordenados = valores.Order().ToList();
        if (ordenados.Count == 0)
        {
            return null;
        }

        var meio = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2;
    }
}
