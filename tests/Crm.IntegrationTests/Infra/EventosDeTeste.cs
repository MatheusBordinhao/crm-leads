using System.Text.Json;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>Eventos para o n8n gravados em <c>eventos_saida</c>.</summary>
public static class EventosDeTeste
{
    /// <summary>Os eventos do lead, do primeiro para o último.</summary>
    public static async Task<List<EventoSaida>> EventosDoLeadAsync(this WebApplicationFactory<Program> fabrica, long leadId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EventosSaida
            .AsNoTracking()
            .Where(e => e.LeadId == leadId)
            .OrderBy(e => e.CriadoEm)
            .ThenBy(e => e.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public static JsonElement Json(this EventoSaida evento) => evento.Payload.RootElement;
}
