using System.Threading.Channels;

namespace Crm.Api.Infra.Importacoes;

/// <summary>As importações criadas nesta instância da API, para o serviço em segundo plano processar na hora.</summary>
public sealed class FilaImportacoes
{
    private readonly Channel<long> _fila = Channel.CreateUnbounded<long>(new UnboundedChannelOptions { SingleReader = true });

    public void Enfileirar(long importacaoId) => _fila.Writer.TryWrite(importacaoId);

    internal ChannelReader<long> Leitor => _fila.Reader;
}

/// <summary>
/// Processa as importações em segundo plano, fora da requisição de quem enviou o arquivo (seção 3.7): na hora, as
/// criadas nesta instância; a cada 30 segundos, as que ficaram com a reserva vencida porque a API parou no meio.
/// </summary>
internal sealed class ServicoImportacoes(
    IServiceScopeFactory escopos,
    FilaImportacoes fila,
    TimeProvider relogio,
    ILogger<ServicoImportacoes> logger) : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var importacaoId = await ProximaDaFilaAsync(stoppingToken);
                await using var escopo = escopos.CreateAsyncScope();
                var processador = escopo.ServiceProvider.GetRequiredService<ProcessadorImportacao>();
                if (importacaoId is { } id)
                {
                    await processador.ProcessarAsync(id, stoppingToken);
                }
                else
                {
                    await processador.RetomarInterrompidasAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception excecao)
            {
                // Uma rodada que falha (o banco fora do ar, por exemplo) não derruba o serviço. A importação fica com a
                // reserva, que vence em um minuto, e uma rodada seguinte continua de onde ela parou.
                logger.LogError(excecao, "Falha ao processar as importações");
            }
        }
    }

    /// <summary>A próxima importação desta instância, esperando até o fim do intervalo. Nula se nenhuma chegou.</summary>
    private async Task<long?> ProximaDaFilaAsync(CancellationToken ct)
    {
        using var espera = new CancellationTokenSource(Intervalo, relogio);
        using var ligado = CancellationTokenSource.CreateLinkedTokenSource(ct, espera.Token);
        try
        {
            return await fila.Leitor.ReadAsync(ligado.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
