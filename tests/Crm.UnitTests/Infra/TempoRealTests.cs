using Crm.Api.Infra.TempoReal;

namespace Crm.UnitTests.Infra;

public class TempoRealTests
{
    /// <summary>Nenhum dado pessoal passa pelo hub: os avisos só levam ids, e a tela busca os dados na API.</summary>
    [Fact]
    public void Os_avisos_do_hub_so_levam_ids()
    {
        var parametros = typeof(IClienteTempoReal).GetMethods().SelectMany(m => m.GetParameters()).ToList();

        Assert.NotEmpty(parametros);
        Assert.All(parametros, p => Assert.Contains(p.ParameterType, new[] { typeof(long), typeof(long[]) }));
    }
}
