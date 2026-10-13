using System.Net;
using Crm.Api.Infra.Web;

namespace Crm.UnitTests.Infra;

public class IpDoClienteTests
{
    [Theory]
    [InlineData("::ffff:172.18.0.1", "172.18.0.1")]
    [InlineData("::FFFF:203.0.113.7", "203.0.113.7")]
    [InlineData("172.18.0.1", "172.18.0.1")]
    [InlineData("2001:db8::7", "2001:db8::7")]
    [InlineData("::1", "::1")]
    public void IPv4_mapeado_em_IPv6_volta_a_ser_IPv4_e_o_resto_fica_como_esta(string ip, string esperado)
    {
        Assert.Equal(esperado, IpDoCliente.Normalizar(IPAddress.Parse(ip))?.ToString());
    }

    [Fact]
    public void Sem_IP_continua_sem_IP()
    {
        Assert.Null(IpDoCliente.Normalizar(null));
        Assert.Null(IpDoCliente.Texto(null));
    }
}
