using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Eventos;

/// <summary>
/// Roda o <see cref="DespachanteEventos"/> em segundo plano, a cada <c>N8n__IntervaloSegundos</c> (padrão: 5 s):
/// os eventos nunca são enviados dentro de uma requisição de usuário. Sem <c>N8n__WebhookUrl</c>, não roda.
/// </summary>
internal sealed class ServicoDespacho(
    IServiceScopeFactory escopos,
    IOptions<N8nOptions> opcoes,
    TimeProvider relogio,
    ILogger<ServicoDespacho> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Value.WebhookUrl))
        {
            logger.LogInformation("N8n__WebhookUrl não está configurada: os eventos para o n8n ficam pendentes.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(opcoes.Value.IntervaloSegundos), relogio);
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<DespachanteEventos>().DespacharAsync(stoppingToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                // Uma rodada que falha (o banco fora do ar, por exemplo) não derruba o serviço: a próxima tenta de novo.
                logger.LogError(excecao, "Falha ao despachar os eventos para o n8n");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
