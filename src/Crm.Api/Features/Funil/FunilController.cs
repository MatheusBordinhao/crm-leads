using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Funil;

/// <summary>Filtros do quadro, na query string, com os mesmos nomes da lista de leads.</summary>
public sealed class FiltroFunil : IFiltroLeads
{
    [FromQuery(Name = "busca")]
    public string? Busca { get; set; }

    [FromQuery(Name = "origem")]
    public Origem? Origem { get; set; }

    [FromQuery(Name = "campanhaId")]
    public int? CampanhaId { get; set; }

    [FromQuery(Name = "responsavelId")]
    public Guid? ResponsavelId { get; set; }

    [FromQuery(Name = "semResponsavel")]
    public bool SemResponsavel { get; set; }
}

/// <summary>Cartão do quadro: o que a atendente precisa para reconhecer o lead, sem e-mail, telefone nem documento.</summary>
public sealed record CartaoFunilResponse(
    long Id, string Nome, string? Empresa, Origem Origem, ResponsavelResumo? Responsavel, bool Teste, DateTimeOffset CriadoEm);

/// <param name="Total">Quantos leads estão na etapa, mesmo os que não cabem na coluna.</param>
/// <param name="Leads">Os mais recentes primeiro, até <see cref="FunilController.CartoesPorColuna"/>.</param>
public sealed record ColunaFunilResponse(EtapaResumo Etapa, int Total, IReadOnlyList<CartaoFunilResponse> Leads);

/// <param name="Periodo">Quantos leads foram para a etapa desde <see cref="QuadroFunilResponse.Desde"/>.</param>
public sealed record FechamentoFunilResponse(EtapaResumo Etapa, int Periodo);

/// <summary>Quadro do funil (seção 7): uma coluna por etapa aberta, e Ganho e Perdido como áreas de soltar.</summary>
/// <param name="Desde">Primeiro dia (horário de Brasília) contado em Ganho e Perdido: os últimos 30 dias, com hoje.</param>
public sealed record QuadroFunilResponse(
    IReadOnlyList<ColunaFunilResponse> Colunas, FechamentoFunilResponse Ganho, FechamentoFunilResponse Perdido, DateOnly Desde);

/// <summary>
/// Quadro do funil. Parte de <see cref="VisibilidadeLeads"/>, como a lista: a atendente só vê os próprios leads.
/// As colunas abertas mostram todos os leads em andamento, de qualquer data, para nenhum lead parado sumir; Ganho e
/// Perdido mostram só quantos foram para lá nos últimos 30 dias.
/// </summary>
[ApiController]
[Route("api/funil")]
[Authorize]
public sealed class FunilController(CrmDbContext db, VisibilidadeLeads visibilidade, TimeProvider relogio) : ControllerBase
{
    public const int CartoesPorColuna = 100;

    public const int DiasDoFechamento = 30;

    [HttpGet]
    public async Task<ActionResult<QuadroFunilResponse>> Obter([FromQuery] FiltroFunil filtro)
    {
        var ct = HttpContext.RequestAborted;
        var etapas = await db.Etapas.AsNoTracking().Where(e => e.Ativa).OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync(ct);
        var leads = FiltrosLeads.Aplicar(visibilidade.Visiveis(), filtro, visibilidade.EhAdmin);

        var abertas = etapas.Where(e => e.Tipo == TipoEtapa.Aberta).ToList();
        var idsAbertas = abertas.Select(e => e.Id).ToList();
        var totais = await leads
            .Where(l => idsAbertas.Contains(l.EtapaId))
            .GroupBy(l => l.EtapaId)
            .Select(g => new { EtapaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(t => t.EtapaId, t => t.Total, ct);

        var colunas = new List<ColunaFunilResponse>();
        foreach (var etapa in abertas)
        {
            var cartoes = await leads
                .Where(l => l.EtapaId == etapa.Id)
                .OrderByDescending(l => l.CriadoEm)
                .ThenByDescending(l => l.Id)
                .Take(CartoesPorColuna)
                .Select(l => new CartaoFunilResponse(
                    l.Id,
                    l.Nome,
                    l.Empresa,
                    l.Origem,
                    l.Responsavel == null ? null : new ResponsavelResumo(l.Responsavel.Id, l.Responsavel.Nome),
                    l.Teste,
                    l.CriadoEm))
                .ToListAsync(ct);
            colunas.Add(new ColunaFunilResponse(Resumo(etapa), totais.GetValueOrDefault(etapa.Id), cartoes));
        }

        var desde = HorarioBrasilia.Dia(relogio.GetUtcNow()).AddDays(-(DiasDoFechamento - 1));
        var inicio = HorarioBrasilia.InicioDoDia(desde);
        var ganho = etapas.Single(e => e.Tipo == TipoEtapa.Ganho);
        var perdido = etapas.Single(e => e.Tipo == TipoEtapa.Perdido);
        var ganhos = await leads.CountAsync(l => l.EtapaId == ganho.Id && l.ConvertidoEm >= inicio, ct);
        var perdidos = await leads.CountAsync(l => l.EtapaId == perdido.Id && l.PerdidoEm >= inicio, ct);

        return new QuadroFunilResponse(
            colunas, new FechamentoFunilResponse(Resumo(ganho), ganhos), new FechamentoFunilResponse(Resumo(perdido), perdidos), desde);
    }

    private static EtapaResumo Resumo(Etapa etapa) => new(etapa.Id, etapa.Nome, etapa.Tipo);
}
