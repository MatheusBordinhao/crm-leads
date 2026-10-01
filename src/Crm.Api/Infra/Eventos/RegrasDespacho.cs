namespace Crm.Api.Infra.Eventos;

/// <summary>Quando tentar de novo a entrega de um evento que falhou (seção 4.3).</summary>
public static class RegrasDespacho
{
    /// <summary>A espera depois da 1ª, da 2ª, ..., da 6ª falha.</summary>
    public static readonly IReadOnlyList<TimeSpan> Esperas =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
    ];

    /// <summary>Tentativas no total: a primeira e uma depois de cada espera.</summary>
    public static int MaximoDeTentativas => Esperas.Count + 1;

    /// <summary>Quanto esperar depois de <paramref name="tentativas"/> tentativas que falharam. Nulo: não tenta mais.</summary>
    public static TimeSpan? EsperaDepoisDe(int tentativas) =>
        tentativas >= 1 && tentativas <= Esperas.Count ? Esperas[tentativas - 1] : null;
}
