using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.UnitTests.Leads;

public class TelefonesTests
{
    [Theory]
    [InlineData("(51) 99999-8888", "+5551999998888")]
    [InlineData("51999998888", "+5551999998888")]
    [InlineData("+55 51 99999-8888", "+5551999998888")]
    [InlineData("5551999998888", "+5551999998888")]
    [InlineData("(11) 3333-4444", "+551133334444")]
    [InlineData("+1 650 253 0000", "+16502530000")]
    public void Numero_reconhecido_vira_E164(string entrada, string esperado)
    {
        var normalizado = Telefones.Normalizar(entrada);

        Assert.Equal(esperado, normalizado);
        Assert.True(Telefones.Valido(normalizado));
    }

    [Theory]
    [InlineData(" 123 ", "123")]
    [InlineData("(51) 9999-888", "(51) 9999-888")]
    [InlineData("não tenho", "não tenho")]
    public void Numero_nao_reconhecido_fica_como_veio_e_nao_e_valido(string entrada, string esperado)
    {
        var normalizado = Telefones.Normalizar(entrada);

        Assert.Equal(esperado, normalizado);
        Assert.False(Telefones.Valido(normalizado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Vazio_vira_nulo(string? entrada)
    {
        Assert.Null(Telefones.Normalizar(entrada));
    }

    [Fact]
    public void So_E164_conta_como_valido()
    {
        Assert.False(Telefones.Valido("(51) 99999-8888"));
    }
}
