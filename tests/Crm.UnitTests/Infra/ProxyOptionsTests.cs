using Crm.Api.Infra.Web;

namespace Crm.UnitTests.Infra;

public class ProxyOptionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("172.30.0.10")]
    [InlineData("fd00::10")]
    public void Proxy__Ip_vazio_ou_um_endereco_IP_e_aceito(string? ip)
    {
        Assert.True(new ValidacaoProxyOptions().Validate(null, new ProxyOptions { Ip = ip }).Succeeded);
    }

    [Theory]
    [InlineData("caddy")]
    [InlineData("172.30.0.0/24")]
    public void Proxy__Ip_que_nao_e_um_endereco_IP_impede_a_API_de_subir(string ip)
    {
        var resultado = new ValidacaoProxyOptions().Validate(null, new ProxyOptions { Ip = ip });

        Assert.True(resultado.Failed);
        Assert.Equal(ValidacaoProxyOptions.Mensagem, resultado.FailureMessage);
    }
}
