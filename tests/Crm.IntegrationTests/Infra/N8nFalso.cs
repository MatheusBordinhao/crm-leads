using System.Collections.Concurrent;
using System.Net;
using Crm.Api.Infra.Eventos;

namespace Crm.IntegrationTests.Infra;

/// <summary>Uma requisição que o CRM mandou ao n8n.</summary>
public sealed record RequisicaoAoN8n(HttpMethod Metodo, Uri Url, string? Chave, string? EventoId, string? TipoDoConteudo, string Corpo);

/// <summary>
/// O n8n dos testes: no lugar do HttpClient do despachante, guarda cada requisição e responde como o teste mandar,
/// inclusive fora do ar (falha de conexão) ou sem responder até o tempo esgotar.
/// </summary>
public sealed class N8nFalso : HttpMessageHandler
{
    public ConcurrentQueue<RequisicaoAoN8n> Recebidas { get; } = new();

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public string CorpoDaResposta { get; set; } = "{}";

    /// <summary>Recusa a conexão, como um n8n fora do ar.</summary>
    public bool ForaDoAr { get; set; }

    /// <summary>Não responde: o envio espera até o tempo limite do despachante.</summary>
    public bool Demorar { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Recebidas.Enqueue(new RequisicaoAoN8n(
            request.Method,
            request.RequestUri!,
            Cabecalho(request, DespachanteEventos.CabecalhoChave),
            Cabecalho(request, DespachanteEventos.CabecalhoEventoId),
            request.Content?.Headers.ContentType?.MediaType,
            request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

        if (ForaDoAr)
        {
            throw new HttpRequestException(HttpRequestError.ConnectionError, "Nenhuma conexão pôde ser feita.");
        }

        if (Demorar)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return new HttpResponseMessage(Status) { Content = new StringContent(CorpoDaResposta) };
    }

    private static string? Cabecalho(HttpRequestMessage request, string nome) =>
        request.Headers.TryGetValues(nome, out var valores) ? valores.Single() : null;
}
