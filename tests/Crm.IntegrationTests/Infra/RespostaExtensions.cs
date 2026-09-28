using System.Text.Json;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Leitura do corpo das respostas. Lê como texto, que vem do buffer a cada chamada, para poder conferir
/// mais de um campo da mesma resposta.
/// </summary>
public static class RespostaExtensions
{
    /// <summary>Lê o corpo como ProblemDetails, conferindo o tipo de conteúdo.</summary>
    public static async Task<JsonElement> LerProblemaAsync(this HttpResponseMessage resposta)
    {
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        return await resposta.LerJsonAsync();
    }

    public static async Task<string?> LerDetalheAsync(this HttpResponseMessage resposta) =>
        (await resposta.LerProblemaAsync()).GetProperty("detail").GetString();

    /// <summary>Mensagens de erro de um campo num ValidationProblem.</summary>
    public static async Task<string[]> LerErrosDoCampoAsync(this HttpResponseMessage resposta, string campo)
    {
        var problema = await resposta.LerProblemaAsync();
        return problema.GetProperty("errors").TryGetProperty(campo, out var erros)
            ? [.. erros.EnumerateArray().Select(e => e.GetString()!)]
            : [];
    }

    public static async Task<JsonElement> LerJsonAsync(this HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
}
