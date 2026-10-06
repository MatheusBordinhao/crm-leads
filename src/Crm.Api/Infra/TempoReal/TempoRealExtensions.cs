namespace Crm.Api.Infra.TempoReal;

internal static class TempoRealExtensions
{
    /// <summary>O SignalR, com os tempos padrão, e a conferência das conexões abertas a cada minuto.</summary>
    public static IServiceCollection AddTempoReal(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddSingleton<ConexoesTempoReal>();
        services.AddHostedService<ServicoConferenciaConexoes>();
        return services;
    }

    public static WebApplication MapTempoReal(this WebApplication app)
    {
        app.MapHub<NotificacoesHub>(NotificacoesHub.Rota);
        return app;
    }
}
