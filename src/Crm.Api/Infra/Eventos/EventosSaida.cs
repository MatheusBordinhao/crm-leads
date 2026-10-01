using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Eventos;

/// <summary>
/// Grava os eventos para o n8n (seção 4.3) na transação da mudança: eles entram no próximo <c>SaveChanges</c> e
/// são entregues depois, em segundo plano. Só entram os tipos de <c>N8n__Eventos</c>, porque cada envio gasta uma
/// execução do plano do n8n, e lead de teste não gera evento.
/// </summary>
public sealed class EventosSaida(CrmDbContext db, IOptions<N8nOptions> n8n, IOptions<CrmOptions> crm, TimeProvider relogio)
{
    /// <summary>Se um evento desse tipo, desse lead, seria gravado.</summary>
    public bool Grava(TipoEvento tipo, Lead lead) => !lead.Teste && n8n.Value.Grava(tipo);

    /// <summary>
    /// Monta o evento com o lead como ele está na transação, inclusive com as mudanças ainda não salvas, e o grava.
    /// O lead precisa ter id, e a primeira entrada dele, se houver, precisa estar salva.
    /// </summary>
    internal async Task RegistrarAsync(TipoEvento tipo, Lead lead, object dados, CancellationToken ct)
    {
        if (!Grava(tipo, lead))
        {
            return;
        }

        var etapa = await EtapaAsync(lead.EtapaId, ct);
        var campanha = lead.CampanhaId is { } campanhaId ? (await db.Campanhas.FindAsync([campanhaId], ct))?.Nome : null;
        var idExterno = await db.EntradasRecebidas
            .Where(e => e.LeadId == lead.Id)
            .OrderBy(e => e.Id)
            .Select(e => e.IdExterno)
            .FirstOrDefaultAsync(ct);
        var responsavel = await db.Users
            .Where(u => u.Id == lead.ResponsavelId && u.Ativo)
            .Select(u => new ResponsavelNoEvento(u.Id, u.Nome, u.Email!))
            .SingleOrDefaultAsync(ct);

        var agora = relogio.GetUtcNow();
        var id = Guid.CreateVersion7(agora);
        var evento = new EventoN8n(
            id,
            tipo,
            agora,
            new LeadNoEvento(
                lead.Id, lead.Nome, lead.Empresa, lead.Uf, lead.Origem, lead.Plataforma, campanha, idExterno, etapa, crm.Value.LinkDoLead(lead.Id)),
            responsavel,
            dados);

        db.EventosSaida.Add(new EventoSaida
        {
            Id = id,
            Tipo = tipo,
            LeadId = lead.Id,
            Payload = JsonSerializer.SerializeToDocument(evento, EventoN8n.Json),
            Situacao = SituacaoEvento.Pendente,
            ProximaTentativaEm = agora,
            CriadoEm = agora,
        });
    }

    /// <summary>A etapa como está no contexto: a de destino de uma mudança já vem carregada, sem outra consulta.</summary>
    internal async Task<EtapaNoEvento> EtapaAsync(int etapaId, CancellationToken ct)
    {
        var etapa = (await db.Etapas.FindAsync([etapaId], ct))!;
        return new EtapaNoEvento(etapa.Id, etapa.Nome, etapa.Tipo);
    }
}

internal static class EventosExtensions
{
    /// <summary>
    /// Configuração do n8n e do endereço do CRM, conferidas na subida, a gravação dos eventos e o despachante em
    /// segundo plano, com o HttpClient do n8n e o tempo limite do envio.
    /// </summary>
    public static IServiceCollection AddEventos(this IServiceCollection services)
    {
        services.AddOptions<CrmOptions>().BindConfiguration(CrmOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<CrmOptions>, ValidacaoCrmOptions>();
        services.AddOptions<N8nOptions>().BindConfiguration(N8nOptions.Secao).ValidateOnStart();
        services.AddSingleton<IValidateOptions<N8nOptions>, ValidacaoN8nOptions>();

        services.AddHttpClient(DespachanteEventos.NomeCliente, (provedor, cliente) =>
            cliente.Timeout = TimeSpan.FromSeconds(provedor.GetRequiredService<IOptions<N8nOptions>>().Value.TempoLimiteSegundos));
        services.AddHostedService<ServicoDespacho>();
        return services.AddScoped<EventosSaida>().AddScoped<DespachanteEventos>();
    }
}
