using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.UnitTests.Leads;

public class EmailsUfsTextosTests
{
    [Theory]
    [InlineData(" Maria@EmpresaX.com.BR ", "maria@empresax.com.br")]
    [InlineData("maria @ empresax.com", "maria@empresax.com")]
    public void Email_fica_em_minusculas_e_sem_espacos(string entrada, string esperado)
    {
        var normalizado = Emails.Normalizar(entrada);

        Assert.Equal(esperado, normalizado);
        Assert.True(Emails.Valido(normalizado));
    }

    [Theory]
    [InlineData("maria")]
    [InlineData("maria@empresax")]
    [InlineData("@empresax.com")]
    public void Email_sem_formato_de_email_nao_e_valido(string entrada)
    {
        Assert.False(Emails.Valido(Emails.Normalizar(entrada)));
    }

    // Os mesmos casos do teste do esquema da tela (web/src/features/leads/esquema.test.ts): os dois formatos são iguais.
    [Theory]
    [InlineData("maria.souza+orcamento@empresa-x.com.br")]
    [InlineData("joao_silva@sub.empresa.com.br")]
    [InlineData("o'connor@example.com")]
    [InlineData("maria@xn--exemplo-9za.com")]
    [InlineData("contato@123.com.br")]
    public void Email_com_os_caracteres_do_RFC_5322_e_valido(string email)
    {
        Assert.True(Emails.Valido(Emails.Normalizar(email)));
    }

    [Theory]
    [InlineData("testenotificação@exemplo.com")]
    [InlineData("maria@exemplação.com")]
    [InlineData("maria,souza@empresa.com")]
    [InlineData("\"maria\"@empresa.com")]
    [InlineData(".maria@empresa.com")]
    [InlineData("maria.@empresa.com")]
    [InlineData("maria..souza@empresa.com")]
    [InlineData("maria@empresa..com")]
    [InlineData("maria@-empresa.com")]
    [InlineData("maria@empresa-.com")]
    [InlineData("maria@empresa_x.com")]
    [InlineData("maria@@empresa.com")]
    public void Email_com_acento_ou_fora_do_formato_comum_nao_e_valido(string email)
    {
        Assert.False(Emails.Valido(Emails.Normalizar(email)));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData(null)]
    public void Email_vazio_vira_nulo(string? entrada)
    {
        Assert.Null(Emails.Normalizar(entrada));
    }

    [Theory]
    [InlineData("RS", "RS")]
    [InlineData(" rs ", "RS")]
    [InlineData("Rio Grande do Sul", "RS")]
    [InlineData("rio  grande do sul", "RS")]
    [InlineData("São Paulo", "SP")]
    [InlineData("SAO PAULO", "SP")]
    [InlineData("Espirito Santo", "ES")]
    public void Uf_aceita_sigla_ou_nome_do_estado(string entrada, string esperado)
    {
        Assert.Equal(esperado, Ufs.Normalizar(entrada));
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("Rio Grande")]
    [InlineData("")]
    [InlineData(null)]
    public void Uf_nao_reconhecida_vira_nulo(string? entrada)
    {
        Assert.Null(Ufs.Normalizar(entrada));
    }

    [Fact]
    public void Sao_27_ufs()
    {
        Assert.Equal(27, Ufs.Siglas.Count());
    }

    [Fact]
    public void Texto_perde_os_espacos_repetidos()
    {
        Assert.Equal("Maria da Silva", Textos.Limpar("  Maria   da\tSilva "));
        Assert.Null(Textos.Limpar("   "));
    }

    [Fact]
    public void Texto_longo_da_integracao_e_cortado()
    {
        Assert.Equal("abc", Textos.LimparECortar("abcdef", 3));
        Assert.Equal("ab", Textos.LimparECortar("ab cdef", 3));
    }
}
