using Crm.Api.Features.Leads;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Painel;

/// <summary>Período do relatório por campanha, na query string. O relatório não tem outros filtros: é a visão da Gama.</summary>
public sealed class FiltroRelatorio : IFiltroPeriodo
{
    /// <summary>Primeiro dia do período, no horário de Brasília.</summary>
    [FromQuery(Name = "de")]
    public DateOnly? De { get; set; }

    /// <summary>Último dia do período, no horário de Brasília.</summary>
    [FromQuery(Name = "ate")]
    public DateOnly? Ate { get; set; }
}

internal sealed class FiltroRelatorioValidator(TimeProvider relogio) : ValidadorPeriodo<FiltroRelatorio>(relogio);

/// <summary>Uma linha do relatório: uma campanha ou, para os leads sem campanha, uma origem.</summary>
/// <param name="Origem">A origem da campanha; nas linhas sem campanha, a dos leads e das entradas da linha.</param>
/// <param name="Campanha">Nula na linha dos leads de uma origem que entraram sem campanha, que não vieram de anúncio.</param>
/// <param name="LeadsNovos">Leads criados no período, pela campanha da primeira entrada.</param>
/// <param name="Entradas">Entradas processadas no período, pela origem e pela campanha de cada uma.</param>
/// <param name="EmAndamento">Leads novos que hoje estão numa etapa aberta.</param>
/// <param name="Ganhos">Leads novos que hoje estão em Ganho.</param>
/// <param name="Perdidos">Leads novos que hoje estão em Perdido.</param>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1; nula sem leads novos.</param>
/// <param name="MedianaPrimeiroContatoMinutos">Nula quando nenhum lead novo da linha teve contato.</param>
public sealed record LinhaRelatorioResponse(
    Origem Origem,
    CampanhaResumo? Campanha,
    int LeadsNovos,
    int Entradas,
    int EmAndamento,
    int Ganhos,
    int Perdidos,
    double? TaxaConversao,
    double? MedianaPrimeiroContatoMinutos);

/// <summary>O total do relatório: os números do painel no mesmo período, sem filtros.</summary>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1; nula sem leads novos.</param>
/// <param name="MedianaPrimeiroContatoMinutos">Nula quando nenhum lead novo teve contato.</param>
public sealed record TotalRelatorioResponse(
    int LeadsNovos,
    int Entradas,
    int EmAndamento,
    int Ganhos,
    int Perdidos,
    double? TaxaConversao,
    double? MedianaPrimeiroContatoMinutos);

/// <summary>
/// Relatório por campanha (seção 8), com as definições do painel. Não leva dados pessoais: só contagens, taxas e os
/// nomes das campanhas.
/// </summary>
/// <param name="De">Primeiro dia do período, no horário de Brasília.</param>
/// <param name="Ate">Último dia do período, no horário de Brasília.</param>
/// <param name="Linhas">
/// Uma por campanha e uma por origem dos leads sem campanha, que vêm sempre no fim; só as que têm algum lead novo ou
/// alguma entrada no período.
/// </param>
public sealed record RelatorioCampanhasResponse(
    DateOnly De, DateOnly Ate, IReadOnlyList<LinhaRelatorioResponse> Linhas, TotalRelatorioResponse Total);
