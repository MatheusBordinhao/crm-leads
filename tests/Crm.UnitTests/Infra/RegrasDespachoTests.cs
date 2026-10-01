using Crm.Api.Infra.Eventos;

namespace Crm.UnitTests.Infra;

public class RegrasDespachoTests
{
    [Fact]
    public void Tenta_de_novo_depois_de_1_min_5_min_30_min_2_h_6_h_e_12_h()
    {
        TimeSpan?[] esperado =
        [
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromHours(2),
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(12),
        ];

        Assert.Equal(esperado, Enumerable.Range(1, 6).Select(RegrasDespacho.EsperaDepoisDe));
    }

    [Fact]
    public void Sao_7_tentativas_e_depois_da_ultima_nao_ha_outra()
    {
        Assert.Equal(7, RegrasDespacho.MaximoDeTentativas);
        Assert.Null(RegrasDespacho.EsperaDepoisDe(RegrasDespacho.MaximoDeTentativas));
        Assert.Null(RegrasDespacho.EsperaDepoisDe(0));
    }
}
