namespace Crm.Api.Infra.Demonstracao;

/// <summary>
/// O comando <c>preparar-demonstracao</c> (README › Demonstração): roda a <see cref="PreparacaoDemonstracao"/> e termina,
/// sem subir a API.
/// </summary>
internal static class ComandoDemonstracao
{
    public const string Nome = "preparar-demonstracao";

    public static bool Pedido(IEnumerable<string> args) => args.Contains(Nome, StringComparer.Ordinal);

    public static IServiceCollection AddDemonstracao(this IServiceCollection services) =>
        services.AddScoped<PreparacaoDemonstracao>();

    /// <summary>Roda a preparação, mostra o resultado e devolve o código de saída: 0 se preparou, 1 se recusou.</summary>
    public static async Task<int> PrepararDemonstracaoAsync(this WebApplication app)
    {
        await using var escopo = app.Services.CreateAsyncScope();
        var resultado = await escopo.ServiceProvider.GetRequiredService<PreparacaoDemonstracao>().ExecutarAsync(CancellationToken.None);
        await (resultado.Preparada ? Console.Out : Console.Error).WriteLineAsync(resultado.Mensagem);
        return resultado.Preparada ? 0 : 1;
    }
}
