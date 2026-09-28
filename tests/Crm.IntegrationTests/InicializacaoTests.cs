using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.IntegrationTests;

public class InicializacaoTests
{
    [Fact]
    public void Sem_connection_string_a_api_nao_sobe_e_explica_o_que_falta()
    {
        using var fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(CrmApiFactory.AmbienteTestes);
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", "");
        });

        var erro = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        Assert.Contains("ConnectionStrings__Crm", erro.GetBaseException().Message);
    }
}
