using System.Collections.Concurrent;
using System.Diagnostics;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.TempoReal;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>Um aviso do hub que chegou ao cliente, com os ids e a hora de chegada.</summary>
public sealed record AvisoRecebido(string Nome, long[] Ids, TimeSpan ChegouEm)
{
    public override string ToString() => $"{Nome}({string.Join(", ", Ids)})";
}

/// <summary>
/// Clientes do hub do tempo real para os testes. O cliente do SignalR fala com o TestServer por Server-Sent Events, com o
/// cookie da sessão no cabeçalho; o WebSocket de verdade é conferido no navegador. O hub e as ações do teste usam a mesma
/// fábrica: uma fábrica do <c>WithWebHostBuilder</c> tem o próprio publicador.
/// </summary>
public static class TempoRealDeTeste
{
    /// <summary>Faz login e devolve o cookie da sessão (<c>crm_sessao=...</c>).</summary>
    public static async Task<string> CookieDaSessaoAsync(
        this WebApplicationFactory<Program> fabrica, Usuario usuario, string senha = UsuariosDeTeste.SenhaPadrao)
    {
        using var cliente = fabrica.CriarClienteHttps();
        var login = await cliente.PostLoginAsync(usuario.Email, senha);
        login.EnsureSuccessStatusCode();
        return login.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{AcessoExtensions.NomeCookie}=", StringComparison.Ordinal))
            .Split(';')[0];
    }

    /// <summary>Conecta ao hub com a sessão do usuário e começa a guardar os avisos.</summary>
    public static async Task<ClienteTempoReal> ConectarAsync(this WebApplicationFactory<Program> fabrica, Usuario usuario) =>
        await fabrica.ConectarComOCookieAsync(await fabrica.CookieDaSessaoAsync(usuario));

    /// <summary>Conecta ao hub com o cookie de uma sessão já aberta. Falha se a conexão não abrir.</summary>
    public static async Task<ClienteTempoReal> ConectarComOCookieAsync(this WebApplicationFactory<Program> fabrica, string cookie)
    {
        var cliente = new ClienteTempoReal(fabrica, cookie);
        await cliente.IniciarAsync();
        return cliente;
    }

    /// <summary>
    /// Tenta conectar, para os testes em que o servidor recusa a conexão: recusada no início ou logo depois dele, o
    /// <see cref="ClienteTempoReal.Fechada"/> completa.
    /// </summary>
    public static async Task<ClienteTempoReal> TentarConectarComOCookieAsync(this WebApplicationFactory<Program> fabrica, string cookie)
    {
        var cliente = new ClienteTempoReal(fabrica, cookie);
        await cliente.TentarIniciarAsync();
        return cliente;
    }

    /// <summary>Roda a conferência das conexões, como o serviço faz a cada minuto.</summary>
    public static async Task ConferirConexoesAsync(this WebApplicationFactory<Program> fabrica)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await fabrica.Services.GetRequiredService<ConexoesTempoReal>()
            .ConferirAsync(escopo.ServiceProvider.GetRequiredService<CrmDbContext>(), TestContext.Current.CancellationToken);
    }
}

/// <summary>Uma conexão com o hub que guarda, na ordem de chegada, os avisos recebidos.</summary>
public sealed class ClienteTempoReal : IAsyncDisposable
{
    /// <summary>Quanto tempo os testes esperam por um aviso: bem mais que os 5 segundos do critério de aceite.</summary>
    public static readonly TimeSpan Espera = TimeSpan.FromSeconds(15);

    private readonly HubConnection _conexao;
    private readonly ConexoesTempoReal _conexoes;
    private readonly ConcurrentQueue<AvisoRecebido> _avisos = new();
    private readonly TaskCompletionSource _fechada = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch _relogio = Stopwatch.StartNew();

    internal ClienteTempoReal(WebApplicationFactory<Program> fabrica, string cookie)
    {
        _conexoes = fabrica.Services.GetRequiredService<ConexoesTempoReal>();
        _conexao = new HubConnectionBuilder()
            .WithUrl(new Uri("https://localhost" + NotificacoesHub.Rota), HttpTransportType.ServerSentEvents, opcoes =>
            {
                opcoes.HttpMessageHandlerFactory = _ => fabrica.Server.CreateHandler();
                opcoes.Headers["Cookie"] = cookie;
            })
            .Build();
        _conexao.On<long[]>(nameof(IClienteTempoReal.LeadsAlterados), ids => Guardar(nameof(IClienteTempoReal.LeadsAlterados), ids));
        _conexao.On<long>(nameof(IClienteTempoReal.NotificacaoNova), id => Guardar(nameof(IClienteTempoReal.NotificacaoNova), [id]));
        _conexao.On(nameof(IClienteTempoReal.FunilAlterado), () => Guardar(nameof(IClienteTempoReal.FunilAlterado), []));
        _conexao.Closed += _ =>
        {
            _fechada.TrySetResult();
            return Task.CompletedTask;
        };
    }

    /// <summary>Os avisos recebidos até agora, na ordem de chegada.</summary>
    public IReadOnlyList<AvisoRecebido> Avisos => [.. _avisos];

    /// <summary>Completa quando o servidor encerra a conexão (ou ela nem chegou a abrir).</summary>
    public Task Fechada => _fechada.Task;

    /// <summary>Desde quando o cliente existe: a régua da hora de chegada dos avisos.</summary>
    public TimeSpan Agora => _relogio.Elapsed;

    /// <summary>Espera um aviso que atenda à condição e o devolve. Falha se ele não chegar em <see cref="Espera"/>.</summary>
    public async Task<AvisoRecebido> AguardarAsync(Func<AvisoRecebido, bool> condicao)
    {
        var limite = Stopwatch.StartNew();
        while (limite.Elapsed < Espera)
        {
            if (_avisos.FirstOrDefault(condicao) is { } aviso)
            {
                return aviso;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"O aviso esperado não chegou. Chegaram: {string.Join("; ", Avisos)}");
    }

    /// <summary>Espera o <c>LeadsAlterados</c> com o id do lead.</summary>
    public Task<AvisoRecebido> AguardarLeadAsync(long leadId) =>
        AguardarAsync(a => a.Nome == nameof(IClienteTempoReal.LeadsAlterados) && a.Ids.Contains(leadId));

    /// <summary>Os avisos que chegaram antes do primeiro que atende à condição (a sentinela).</summary>
    public async Task<List<AvisoRecebido>> AvisosAteAsync(Func<AvisoRecebido, bool> sentinela)
    {
        var chegada = await AguardarAsync(sentinela);
        return [.. Avisos.TakeWhile(a => !ReferenceEquals(a, chegada))];
    }

    /// <summary>Algum aviso, de qualquer tipo, trouxe o id.</summary>
    public bool RecebeuOId(long id) => Avisos.Any(a => a.Nome != nameof(IClienteTempoReal.NotificacaoNova) && a.Ids.Contains(id));

    /// <summary>Espera o servidor encerrar a conexão. Falha se ela continuar aberta depois de <see cref="Espera"/>.</summary>
    public async Task AguardarFechamentoAsync() => await Fechada.WaitAsync(Espera, TestContext.Current.CancellationToken);

    /// <summary>Conecta e espera o hub pôr a conexão nos grupos.</summary>
    internal async Task IniciarAsync()
    {
        await _conexao.StartAsync(TestContext.Current.CancellationToken);
        var limite = Stopwatch.StartNew();
        while (!_conexoes.Registrada(_conexao.ConnectionId!))
        {
            if (limite.Elapsed > Espera || Fechada.IsCompleted)
            {
                throw new InvalidOperationException("O hub não pôs a conexão nos grupos.");
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    internal async Task TentarIniciarAsync()
    {
        try
        {
            await _conexao.StartAsync(TestContext.Current.CancellationToken);
        }
        catch (Exception) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            // O Closed só dispara depois de um início que deu certo: uma recusa durante o início também conta como fechada.
            _fechada.TrySetResult();
        }
    }

    public async ValueTask DisposeAsync() => await _conexao.DisposeAsync();

    private void Guardar(string nome, long[] ids) => _avisos.Enqueue(new AvisoRecebido(nome, ids, _relogio.Elapsed));
}
