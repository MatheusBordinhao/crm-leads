using System.Collections.Concurrent;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.TempoReal;

/// <summary>Como o usuário está no banco agora: o que decide se a conexão dele vale e em que grupos ela fica.</summary>
internal sealed record SituacaoUsuario(Guid Id, bool Ativo, bool TrocarSenha, string? SecurityStamp, bool EhAdmin)
{
    /// <summary>
    /// A conexão aberta com <paramref name="carimbo"/> (o security stamp do cookie) vale: o usuário está ativo, sem
    /// senha provisória, e a senha não foi redefinida depois do login.
    /// </summary>
    public bool Aceita(string? carimbo) => Ativo && !TrocarSenha && carimbo is not null && carimbo == SecurityStamp;
}

/// <summary>Uma conexão aberta: de quem é, com que security stamp ela entrou e se entrou no grupo dos admins.</summary>
internal sealed record ConexaoAberta(HubCallerContext Contexto, Guid UsuarioId, string? Carimbo, bool EhAdmin);

internal static class SituacoesUsuarios
{
    public static Task<List<SituacaoUsuario>> SituacoesAsync(this CrmDbContext db, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new SituacaoUsuario(u.Id, u.Ativo, u.TrocarSenha, u.SecurityStamp, db.Admins().Any(a => a.Id == u.Id)))
            .ToListAsync(ct);
}

/// <summary>
/// As conexões abertas do hub. A sessão é conferida no banco a cada minuto (seção 9), mas só nas requisições, e um
/// WebSocket aberto não faz requisição. Por isso, no mesmo intervalo, <see cref="ConferirAsync"/> encerra a conexão de
/// quem foi desativado, teve a senha redefinida (o security stamp muda) ou mudou de perfil. A tela conecta de novo:
/// com a sessão encerrada, vai para o login; com o perfil trocado, entra só nos grupos do perfil novo.
/// </summary>
public sealed class ConexoesTempoReal(ILogger<ConexoesTempoReal> logger)
{
    private readonly ConcurrentDictionary<string, ConexaoAberta> _abertas = new();

    public int Quantidade => _abertas.Count;

    internal void Registrar(ConexaoAberta conexao) => _abertas[conexao.Contexto.ConnectionId] = conexao;

    internal void Remover(string connectionId) => _abertas.TryRemove(connectionId, out _);

    /// <summary>
    /// A conexão já está nos grupos. O cliente termina o início antes de o hub pôr a conexão nos grupos, então um aviso
    /// enviado nesse meio-tempo não chega a ela; os testes esperam por isto antes de mandar avisos.
    /// </summary>
    internal bool Registrada(string connectionId) => _abertas.ContainsKey(connectionId);

    /// <summary>Encerra as conexões que não valem mais e devolve quantas encerrou.</summary>
    public async Task<int> ConferirAsync(CrmDbContext db, CancellationToken ct)
    {
        var abertas = _abertas.Values.ToList();
        if (abertas.Count == 0)
        {
            return 0;
        }

        var situacoes = (await db.SituacoesAsync([.. abertas.Select(c => c.UsuarioId).Distinct()], ct)).ToDictionary(s => s.Id);
        var encerradas = 0;
        foreach (var conexao in abertas)
        {
            var vale = situacoes.GetValueOrDefault(conexao.UsuarioId) is { } situacao
                && situacao.Aceita(conexao.Carimbo)
                && situacao.EhAdmin == conexao.EhAdmin;
            if (vale)
            {
                continue;
            }

            Remover(conexao.Contexto.ConnectionId);
            conexao.Contexto.Abort();
            encerradas++;
            logger.LogInformation("Conexão do tempo real do usuário {UsuarioId} encerrada: a sessão mudou", conexao.UsuarioId);
        }

        return encerradas;
    }
}

/// <summary>Confere as conexões abertas no intervalo da conferência da sessão (1 minuto).</summary>
internal sealed class ServicoConferenciaConexoes(
    IServiceScopeFactory escopos,
    ConexoesTempoReal conexoes,
    IOptions<SecurityStampValidatorOptions> sessao,
    TimeProvider relogio,
    ILogger<ServicoConferenciaConexoes> logger) : BackgroundService
{
    /// <summary>A sessão pode ser conferida em toda requisição (intervalo zero); as conexões, no máximo a cada segundo.</summary>
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = sessao.Value.ValidationInterval < IntervaloMinimo ? IntervaloMinimo : sessao.Value.ValidationInterval;
        using var timer = new PeriodicTimer(intervalo, relogio);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (conexoes.Quantidade == 0)
            {
                continue;
            }

            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await conexoes.ConferirAsync(escopo.ServiceProvider.GetRequiredService<CrmDbContext>(), stoppingToken);
            }
            catch (Exception excecao) when (excecao is not OperationCanceledException)
            {
                // Uma rodada que falha (o banco fora do ar, por exemplo) não derruba o serviço: a próxima tenta de novo.
                logger.LogError(excecao, "Falha ao conferir as conexões do tempo real");
            }
        }
    }
}
