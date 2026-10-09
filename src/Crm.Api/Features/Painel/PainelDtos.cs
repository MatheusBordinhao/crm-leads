using Crm.Api.Features.Leads;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Web;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Painel;

/// <summary>O período de uma consulta do painel ou do relatório por campanha, em dias no horário de Brasília.</summary>
public interface IFiltroPeriodo
{
    /// <summary>Primeiro dia do período.</summary>
    DateOnly? De { get; }

    /// <summary>Último dia do período.</summary>
    DateOnly? Ate { get; }
}

/// <summary>Filtros do painel, na query string, com os mesmos nomes da lista de leads.</summary>
public sealed class FiltroPainel : IFiltroLeads, IFiltroPeriodo
{
    /// <summary>Primeiro dia do período, no horário de Brasília. Sem ele, 29 dias antes do último.</summary>
    [FromQuery(Name = "de")]
    public DateOnly? De { get; set; }

    /// <summary>Último dia do período, no horário de Brasília. Sem ele, hoje.</summary>
    [FromQuery(Name = "ate")]
    public DateOnly? Ate { get; set; }

    /// <summary>A origem da primeira entrada do lead; nas entradas, a de cada uma.</summary>
    [FromQuery(Name = "origem")]
    public Origem? Origem { get; set; }

    /// <summary>A campanha da primeira entrada do lead; nas entradas, a de cada uma.</summary>
    [FromQuery(Name = "campanhaId")]
    public int? CampanhaId { get; set; }

    /// <summary>Só para o admin.</summary>
    [FromQuery(Name = "responsavelId")]
    public Guid? ResponsavelId { get; set; }

    /// <summary>Só para o admin: leads que ainda não têm responsável.</summary>
    [FromQuery(Name = "semResponsavel")]
    public bool SemResponsavel { get; set; }

    /// <summary>O painel não tem busca.</summary>
    string? IFiltroLeads.Busca => null;
}

/// <summary>
/// As regras do período, as mesmas no painel e no relatório por campanha (seção 8): datas de 2000 a 2099, e o fim não
/// vem antes do início. Sem datas, o período é sempre válido, qualquer que seja o padrão de quem consulta.
/// </summary>
internal abstract class ValidadorPeriodo<T> : Validador<T>
    where T : IFiltroPeriodo
{
    protected ValidadorPeriodo(TimeProvider relogio)
    {
        RuleFor(f => f.De).Must(HorarioBrasilia.DataAceita).WithMessage(HorarioBrasilia.MensagemData);
        RuleFor(f => f.Ate).Must(HorarioBrasilia.DataAceita).WithMessage(HorarioBrasilia.MensagemData);
        RuleFor(f => f.Ate)
            .Must((filtro, _) =>
            {
                var periodo = PeriodoPainel.Resolver(filtro.De, filtro.Ate, HorarioBrasilia.Dia(relogio.GetUtcNow()));
                return periodo.De <= periodo.Ate;
            })
            .When(f => HorarioBrasilia.DataAceita(f.De) && HorarioBrasilia.DataAceita(f.Ate))
            .WithMessage(PeriodoPainel.MensagemPeriodo);
    }
}

internal sealed class FiltroPainelValidator(TimeProvider relogio) : ValidadorPeriodo<FiltroPainel>(relogio);

/// <param name="MedianaMinutos">
/// Mediana do tempo entre a entrada e o primeiro contato, em minutos, dos leads novos que já tiveram contato. Nula
/// quando nenhum teve.
/// </param>
/// <param name="SemContato">Leads novos que ainda não tiveram contato.</param>
public sealed record PrimeiroContatoPainelResponse(double? MedianaMinutos, int ComContato, int SemContato);

/// <param name="LeadsNovos">Leads novos do período que hoje estão na etapa.</param>
public sealed record EtapaPainelResponse(EtapaResumo Etapa, int LeadsNovos);

/// <param name="Origem">A origem da primeira entrada.</param>
/// <param name="Ganhos">Leads novos da origem que hoje estão em Ganho.</param>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1.</param>
public sealed record OrigemPainelResponse(Origem Origem, int LeadsNovos, int Ganhos, double? TaxaConversao);

/// <param name="Campanha">
/// A campanha da primeira entrada; nula para os leads que entraram sem campanha, que não vieram de anúncio e vêm
/// sempre por último.
/// </param>
/// <param name="Ganhos">Leads novos da campanha que hoje estão em Ganho.</param>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1.</param>
public sealed record CampanhaPainelResponse(CampanhaResumo? Campanha, int LeadsNovos, int Ganhos, double? TaxaConversao);

/// <param name="Motivo">Nulo só para uma ida para Perdido gravada sem motivo, o que a API não permite.</param>
/// <param name="Leads">Leads que foram para Perdido com esse motivo no período.</param>
public sealed record MotivoPerdaPainelResponse(MotivoPerdaResumo? Motivo, int Leads);

/// <summary>
/// Leads que foram para Perdido no período, pelo histórico de etapas: de qualquer data de entrada, mesmo os que
/// depois foram reabertos.
/// </summary>
/// <param name="Leads">Quantos leads diferentes: um lead perdido duas vezes no período conta uma vez.</param>
/// <param name="PorMotivo">Cada lead conta uma vez em cada motivo com que foi perdido no período.</param>
public sealed record PerdasPainelResponse(int Leads, IReadOnlyList<MotivoPerdaPainelResponse> PorMotivo);

/// <param name="Ativo">Falso para quem foi desativada: os leads continuam contando para ela.</param>
public sealed record ResponsavelPainelResponse(Guid Id, string Nome, bool Ativo);

/// <param name="Responsavel">A responsável atual dos leads; nula para os leads sem responsável.</param>
/// <param name="MedianaPrimeiroContatoMinutos">Nula quando nenhum lead novo dela teve contato.</param>
public sealed record AtendentePainelResponse(
    ResponsavelPainelResponse? Responsavel, int LeadsNovos, int Ganhos, double? TaxaConversao, double? MedianaPrimeiroContatoMinutos);

/// <summary>
/// Painel (seção 8). Os leads novos são os criados no período; a situação atual, a conversão, o primeiro contato e
/// as quebras por origem, campanha e atendente são todos sobre eles. Os leads de teste ficam de fora de tudo, e os
/// anonimizados continuam contando.
/// </summary>
/// <param name="De">Primeiro dia do período, no horário de Brasília.</param>
/// <param name="Ate">Último dia do período, no horário de Brasília.</param>
/// <param name="Entradas">
/// Entradas que o CRM recebeu no período, incluindo as de pessoas que já eram leads, com a origem e a campanha de
/// cada entrada.
/// </param>
/// <param name="Ganhos">Leads novos que hoje estão em Ganho.</param>
/// <param name="TaxaConversao">Ganhos divididos pelos leads novos, de 0 a 1; nula sem leads novos.</param>
/// <param name="SituacaoAtual">Todas as etapas ativas, na ordem do funil, mesmo as sem leads.</param>
/// <param name="PorAtendente">Só para o admin; nulo para a atendente.</param>
public sealed record PainelResponse(
    DateOnly De,
    DateOnly Ate,
    int LeadsNovos,
    int Entradas,
    int Ganhos,
    double? TaxaConversao,
    PrimeiroContatoPainelResponse PrimeiroContato,
    IReadOnlyList<EtapaPainelResponse> SituacaoAtual,
    IReadOnlyList<OrigemPainelResponse> PorOrigem,
    IReadOnlyList<CampanhaPainelResponse> PorCampanha,
    PerdasPainelResponse Perdas,
    IReadOnlyList<AtendentePainelResponse>? PorAtendente);
