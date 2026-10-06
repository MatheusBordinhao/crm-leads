using Crm.Api.Infra.Dados;

namespace Crm.Api.Infra.TempoReal;

internal static class TempoRealExtensions
{
    /// <summary>
    /// O SignalR, com os tempos padrão, a conferência das conexões abertas a cada minuto e os avisos depois de cada
    /// commit: o interceptador no <see cref="CrmDbContext"/>, a fila e o publicador.
    /// </summary>
    public static IServiceCollection AddTempoReal(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<ConexoesTempoReal>();
        services.AddHostedService<ServicoConferenciaConexoes>();

        services.AddSingleton<FilaTempoReal>();
        services.AddSingleton<InterceptadorTempoReal>();
        services.ConfigureDbContext<CrmDbContext>((provedor, opcoes) =>
            opcoes.AddInterceptors(provedor.GetRequiredService<InterceptadorTempoReal>()));
        services.AddHostedService<PublicadorTempoReal>();
        return services;
    }

    public static WebApplication MapTempoReal(this WebApplication app)
    {
        app.MapHub<NotificacoesHub>(NotificacoesHub.Rota);
        return app;
    }
}
