using System.Threading.Channels;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.TempoReal;

/// <summary>O que mudou, na ordem dos commits. O <see cref="InterceptadorTempoReal"/> escreve, o publicador lê.</summary>
public sealed class FilaTempoReal
{
    private readonly Channel<MudancasTempoReal> _fila =
        Channel.CreateUnbounded<MudancasTempoReal>(new UnboundedChannelOptions { SingleReader = true });

    public void Publicar(MudancasTempoReal mudancas) => _fila.Writer.TryWrite(mudancas);

    internal ChannelReader<MudancasTempoReal> Leitor => _fila.Reader;
}

/// <summary>
/// Manda os avisos do tempo real, fora das requisições e das transações. Depois do primeiro item da fila, espera um
/// instante para juntar o que chegou junto (uma importação de 5.000 linhas faz 5.000 commits), lê a responsável atual de
/// cada lead e manda cada aviso só para quem pode ver o lead (<see cref="EnvioTempoReal"/>). Uma falha, como o banco fora
/// do ar, perde só aquele lote: as telas recarregam tudo quando reconectam ou voltam ao foco.
/// </summary>
internal sealed class PublicadorTempoReal(
    FilaTempoReal fila,
    IServiceScopeFactory escopos,
    IHubContext<NotificacoesHub, IClienteTempoReal> hub,
    TimeProvider relogio,
    ILogger<PublicadorTempoReal> logger) : BackgroundService
{
    /// <summary>Quanto o publicador espera, depois do primeiro item, para juntar o que chegou junto.</summary>
    public static readonly TimeSpan Espera = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await fila.Leitor.WaitToReadAsync(stoppingToken))
            {
                await Task.Delay(Espera, relogio, stoppingToken);
                var lote = new List<MudancasTempoReal>();
                while (fila.Leitor.TryRead(out var mudancas))
                {
                    lote.Add(mudancas);
                }

                try
                {
                    await EnviarAsync(lote, stoppingToken);
                }
                catch (Exception excecao) when (excecao is not OperationCanceledException)
                {
                    logger.LogError(excecao, "Falha ao enviar os avisos do tempo real");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EnviarAsync(List<MudancasTempoReal> lote, CancellationToken ct)
    {
        var envio = EnvioTempoReal.Montar(lote, await LerLeadsAsync(lote, ct));
        if (envio.LeadsParaAdmins.Length > 0)
        {
            await hub.Clients.Group(GruposTempoReal.Admins).LeadsAlterados(envio.LeadsParaAdmins);
        }

        foreach (var (usuarioId, leads) in envio.LeadsPorUsuario)
        {
            await hub.Clients.Group(GruposTempoReal.DoUsuario(usuarioId)).LeadsAlterados(leads);
        }

        foreach (var usuarioId in envio.RecarregarTudo)
        {
            await hub.Clients.Group(GruposTempoReal.DoUsuario(usuarioId)).LeadsAlterados([]);
        }

        foreach (var notificacao in envio.Notificacoes)
        {
            await hub.Clients.Group(GruposTempoReal.DoUsuario(notificacao.UsuarioId)).NotificacaoNova(notificacao.Id);
        }

        if (envio.Funil)
        {
            await hub.Clients.All.FunilAlterado();
        }
    }

    /// <summary>A responsável atual e a marca de teste de cada lead do lote, lidas agora.</summary>
    private async Task<Dictionary<long, LeadNoEnvio>> LerLeadsAsync(List<MudancasTempoReal> lote, CancellationToken ct)
    {
        var ids = lote.SelectMany(m => m.Leads.Concat(m.Trocas.Select(t => t.LeadId))).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        await using var escopo = escopos.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new LeadNoEnvio(l.ResponsavelId, l.Teste), ct);
    }
}
