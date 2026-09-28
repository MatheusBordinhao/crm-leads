using System.Net;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

public class SaudeTests(CrmApiFactory fabrica)
{
    [Fact]
    public async Task Health_responde_200_com_o_banco_no_ar()
    {
        var ct = TestContext.Current.CancellationToken;
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(ct));
    }
}
