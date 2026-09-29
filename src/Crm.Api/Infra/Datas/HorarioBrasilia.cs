namespace Crm.Api.Infra.Datas;

/// <summary>Datas do banco ficam em UTC; os períodos que a pessoa escolhe são dias no horário de Brasília.</summary>
public static class HorarioBrasilia
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    /// <summary>O instante, em UTC, em que o dia começa em Brasília (00:00).</summary>
    public static DateTimeOffset InicioDoDia(DateOnly dia)
    {
        var meiaNoite = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(meiaNoite, Fuso.GetUtcOffset(meiaNoite)).ToUniversalTime();
    }

    /// <summary>O dia em Brasília de um instante.</summary>
    public static DateOnly Dia(DateTimeOffset instante) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Fuso).DateTime);
}
