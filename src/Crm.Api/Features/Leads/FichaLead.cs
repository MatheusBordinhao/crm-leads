using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>A ficha do lead (<see cref="LeadResponse"/>), lida sempre pelos leads que quem pede pode ver.</summary>
internal static class FichaLead
{
    /// <param name="visiveis">Os leads de <see cref="VisibilidadeLeads.Visiveis"/>: o de outra pessoa não é achado.</param>
    public static async Task<LeadResponse?> BuscarAsync(IQueryable<Lead> visiveis, long id, CancellationToken ct)
    {
        var lead = await visiveis
            .AsNoTracking()
            .Include(l => l.Etapa)
            .Include(l => l.Responsavel)
            .Include(l => l.Campanha)
            .Include(l => l.MotivoPerda)
            .SingleOrDefaultAsync(l => l.Id == id, ct);
        return lead is null ? null : De(lead);
    }

    private static LeadResponse De(Lead lead) => new(
        lead.Id,
        lead.Nome,
        lead.Email,
        lead.Telefone,
        lead.CpfCnpj,
        lead.Uf,
        lead.Empresa,
        new EtapaResumo(lead.Etapa.Id, lead.Etapa.Nome, lead.Etapa.Tipo),
        lead.Responsavel is null ? null : new ResponsavelResumo(lead.Responsavel.Id, lead.Responsavel.Nome),
        lead.Origem,
        lead.Plataforma,
        lead.Campanha is null ? null : new CampanhaResumo(lead.Campanha.Id, lead.Campanha.Nome),
        lead.UtmSource,
        lead.UtmMedium,
        lead.UtmCampaign,
        lead.UtmContent,
        lead.UtmTerm,
        lead.Gclid,
        RespostasExtras(lead.RespostasExtras),
        lead.Teste,
        lead.CriadoEm,
        lead.AtualizadoEm,
        lead.PrimeiroContatoEm,
        lead.ConvertidoEm,
        lead.PerdidoEm,
        lead.MotivoPerda is null ? null : new MotivoPerdaResumo(lead.MotivoPerda.Id, lead.MotivoPerda.Nome),
        lead.MotivoPerdaDetalhe,
        lead.AnonimizadoEm,
        AvisosLead.Calcular(lead.Email, lead.Telefone, lead.CpfCnpj),
        lead.Versao);

    private static List<RespostaExtra> RespostasExtras(JsonDocument? respostas) =>
        respostas?.RootElement.ValueKind == JsonValueKind.Object
            ? [.. respostas.RootElement.EnumerateObject().Select(p => new RespostaExtra(
                p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText()))]
            : [];
}
