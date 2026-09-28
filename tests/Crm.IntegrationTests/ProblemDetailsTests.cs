using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

public class ProblemDetailsTests(CrmApiFactory fabrica)
{
    [Fact]
    public async Task Rota_inexistente_responde_problem_details_em_portugues()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/api/nao-existe", ct);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(404, problema.GetProperty("status").GetInt32());
        Assert.Equal("Recurso não encontrado", problema.GetProperty("title").GetString());
        Assert.True(problema.TryGetProperty("traceId", out _));
    }
}
