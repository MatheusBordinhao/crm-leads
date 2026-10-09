namespace Crm.Api.Infra.Datas;

/// <summary>Datas do banco ficam em UTC; os períodos que a pessoa escolhe são dias no horário de Brasília.</summary>
public static class HorarioBrasilia
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public const string MensagemData = "Informe uma data entre 01/01/2000 e 31/12/2099.";

    /// <summary>
    /// As datas aceitas nos filtros por dia (painel, relatório, lista de leads e auditoria): o CRM não tem nada antes, e
    /// as extremas estourariam o <see cref="DateOnly"/> na conta do fim do período.
    /// </summary>
    public static readonly DateOnly MenorData = new(2000, 1, 1);

    public static readonly DateOnly MaiorData = new(2099, 12, 31);

    public static bool DataAceita(DateOnly? dia) => dia is null || (dia >= MenorData && dia <= MaiorData);

    /// <summary>O instante, em UTC, em que o dia começa em Brasília (00:00).</summary>
    public static DateTimeOffset InicioDoDia(DateOnly dia)
    {
        var meiaNoite = dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(meiaNoite, Fuso.GetUtcOffset(meiaNoite)).ToUniversalTime();
    }

    /// <summary>O dia em Brasília de um instante.</summary>
    public static DateOnly Dia(DateTimeOffset instante) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, Fuso).DateTime);
}
