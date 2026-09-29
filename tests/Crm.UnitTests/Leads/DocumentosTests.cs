using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.UnitTests.Leads;

public class DocumentosTests
{
    [Theory]
    [InlineData("12ABC34501DE35")] // CNPJ alfanumérico
    [InlineData("11222333000181")] // CNPJ numérico
    [InlineData("52998224725")] // CPF
    public void Valores_de_teste_da_especificacao_sao_validos(string documento)
    {
        Assert.True(Documentos.Valido(Documentos.Normalizar(documento)));
    }

    [Theory]
    [InlineData("12ABC34501DE35")]
    [InlineData("11222333000181")]
    [InlineData("52998224725")]
    public void Trocar_o_ultimo_digito_invalida(string documento)
    {
        var ultimo = documento[^1] - '0';
        var trocado = documento[..^1] + (char)('0' + ((ultimo + 1) % 10));

        Assert.False(Documentos.Valido(Documentos.Normalizar(trocado)));
    }

    [Theory]
    [InlineData("12.ABC.345/01DE-35", "12ABC34501DE35")]
    [InlineData("12abc34501de35", "12ABC34501DE35")]
    [InlineData(" 529.982.247-25 ", "52998224725")]
    [InlineData("11.222.333/0001-81", "11222333000181")]
    public void Normaliza_tirando_a_mascara_e_passando_para_maiusculas(string entrada, string esperado)
    {
        var normalizado = Documentos.Normalizar(entrada);

        Assert.Equal(esperado, normalizado);
        Assert.True(Documentos.Valido(normalizado));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ./- ")]
    [InlineData(null)]
    public void Vazio_vira_nulo(string? entrada)
    {
        Assert.Null(Documentos.Normalizar(entrada));
    }

    [Theory]
    [InlineData("11111111111")] // sequência repetida
    [InlineData("00000000000000")]
    [InlineData("1234567890")] // 10 caracteres
    [InlineData("5299822472A")] // CPF com letra
    [InlineData("12ABC34501DEAB")] // CNPJ com letra nos dígitos verificadores
    [InlineData(null)]
    public void Formatos_invalidos_nao_passam(string? documento)
    {
        Assert.False(Documentos.Valido(documento));
    }
}
