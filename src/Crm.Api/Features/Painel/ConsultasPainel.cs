using Crm.Api.Features.Leads;
using Crm.Api.Infra.Dados;

namespace Crm.Api.Features.Painel;

/// <summary>
/// As consultas que o painel e o relatório por campanha têm em comum (seção 8): os leads novos do período e as entradas
/// que o CRM processou nele. Quem chama já tirou os leads de teste e aplicou a visibilidade.
/// </summary>
internal static class ConsultasPainel
{
    /// <summary>Os leads criados no período, só com o que as métricas usam.</summary>
    public static IQueryable<LeadDaCoorte> Coorte(IQueryable<Lead> leads, PeriodoPainel periodo)
    {
        var inicio = periodo.Inicio;
        var fim = periodo.Fim;
        return leads
            .Where(l => l.CriadoEm >= inicio && l.CriadoEm < fim)
            .Select(l => new LeadDaCoorte(l.EtapaId, l.Etapa.Tipo, l.ResponsavelId, l.Origem, l.CampanhaId, l.CriadoEm, l.PrimeiroContatoEm));
    }

    /// <summary>
    /// As entradas que o CRM processou no período, incluindo as de pessoas que já eram leads, dos leads de
    /// <paramref name="donos"/>. A hora é a do processamento, a mesma do <c>criado_em</c> do lead novo: o
    /// <c>recebidoEm</c> vem da integração e chega atrasado num reenvio.
    /// </summary>
    public static IQueryable<EntradaRecebida> Entradas(CrmDbContext db, IQueryable<Lead> donos, PeriodoPainel periodo)
    {
        var inicio = periodo.Inicio;
        var fim = periodo.Fim;
        return db.EntradasRecebidas.Where(e =>
            e.ProcessadoEm >= inicio && e.ProcessadoEm < fim && !e.Teste && donos.Any(l => l.Id == e.LeadId));
    }
}
