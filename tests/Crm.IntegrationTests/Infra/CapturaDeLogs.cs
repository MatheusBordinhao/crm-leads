using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Guarda tudo o que a API registra, em todos os níveis e com os escopos, para os testes conferirem que
/// nenhum dado pessoal vai para o log.
/// </summary>
public sealed class CapturaDeLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _registros = new();

    public IReadOnlyCollection<string> Registros => _registros;

    public ILogger CreateLogger(string categoryName) => new Registrador(categoryName, _registros);

    public void Dispose()
    {
    }

    private sealed class Registrador(string categoria, ConcurrentQueue<string> registros) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            registros.Enqueue($"Escopo {categoria}: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            registros.Enqueue($"{logLevel} {categoria}: {formatter(state, exception)} {exception}");
    }
}
