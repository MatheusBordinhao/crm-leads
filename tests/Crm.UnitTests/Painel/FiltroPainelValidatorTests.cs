using Crm.Api.Features.Painel;
using Crm.Api.Infra.Datas;

namespace Crm.UnitTests.Painel;

public class FiltroPainelValidatorTests
{
    /// <summary>Já é dia 6 em UTC, mas ainda é dia 5 em Brasília.</summary>
    private static readonly DateTimeOffset Agora = new(2026, 10, 6, 2, 30, 0, TimeSpan.Zero);

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private static IEnumerable<(string, string)> Erros(DateOnly? de, DateOnly? ate) =>
        new FiltroPainelValidator(new RelogioFixo())
            .Validate(new FiltroPainel { De = de, Ate = ate })
            .Errors.Select(e => (e.PropertyName, e.ErrorMessage));

    [Fact]
    public void Periodo_sem_datas_ou_com_inicio_e_fim_em_ordem_vale()
    {
        Assert.Empty(Erros(null, null));
        Assert.Empty(Erros(new(2026, 9, 1), new(2026, 9, 30)));
        Assert.Empty(Erros(new(2026, 9, 30), new(2026, 9, 30)));
        Assert.Empty(Erros(new(2026, 10, 5), null));
    }

    [Fact]
    public void Fim_antes_do_inicio_e_recusado_mesmo_quando_o_fim_e_hoje_em_Brasilia()
    {
        Assert.Equal([("Ate", PeriodoPainel.MensagemPeriodo)], Erros(new(2026, 9, 30), new(2026, 9, 1)));
        Assert.Equal([("Ate", PeriodoPainel.MensagemPeriodo)], Erros(new(2026, 10, 6), null));
    }

    [Fact]
    public void Datas_fora_de_2000_a_2099_sao_recusadas()
    {
        Assert.Equal([("De", HorarioBrasilia.MensagemData)], Erros(new(1999, 12, 31), new(2026, 9, 30)));
        Assert.Equal([("Ate", HorarioBrasilia.MensagemData)], Erros(null, DateOnly.MaxValue));
        Assert.Empty(Erros(new(2000, 1, 1), new(2099, 12, 31)));
    }
}
