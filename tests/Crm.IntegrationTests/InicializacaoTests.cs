using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Crm.IntegrationTests;

public class InicializacaoTests(CrmApiFactory fabrica)
{
    [Fact]
    public void Sem_connection_string_a_api_nao_sobe_e_explica_o_que_falta()
    {
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(CrmApiFactory.AmbienteTestes);
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", "");
        });

        var erro = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("ConnectionStrings__Crm", erro.GetBaseException().Message);
    }

    [Fact]
    public async Task Sem_admin_e_sem_Admin__Email_a_api_nao_sobe_e_explica_o_que_falta()
    {
        var conexao = await fabrica.CriarBancoAsync("sem_admin");
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(CrmApiFactory.AmbienteTestes);
            builder.UseSetting($"ConnectionStrings:{DadosExtensions.NomeConexao}", conexao);
            builder.UseSetting("Admin:Email", "");
            builder.UseSetting("Admin:SenhaInicial", "");
        });

        var erro = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("Admin__Email", erro.GetBaseException().Message);
    }
}
