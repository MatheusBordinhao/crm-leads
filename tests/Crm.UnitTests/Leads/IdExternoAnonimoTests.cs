using Crm.Api.Features.Leads;

namespace Crm.UnitTests.Leads;

public class IdExternoAnonimoTests
{
    /// <summary>
    /// O valor fixo garante que o hash nunca muda: as entradas já anonimizadas guardam só ele, e um hash diferente faria o
    /// reenvio de um lead anonimizado criar a pessoa de novo.
    /// </summary>
    [Fact]
    public void E_o_prefixo_e_o_SHA256_do_id_em_hexadecimal()
    {
        Assert.Equal(
            "anon:7a51d064a1a216a692f753fcdab276e4ff201a01d8b66f56d50d4d719fd0dc87",
            IdExternoAnonimo.De("1234567890123456"));
    }

    [Fact]
    public void O_id_da_importacao_sem_coluna_de_id_tambem_vira_hash()
    {
        Assert.Equal(
            "anon:67e9bc3cfd2163c2978358dfe00d2f912cd4ee0c99f077c3583b39b48aebb124",
            IdExternoAnonimo.De("sha256:abc"));
    }

    [Fact]
    public void Cabe_no_tamanho_do_id_externo_mesmo_para_o_maior_id()
    {
        var anonimo = IdExternoAnonimo.De(new string('9', Campanha.TamanhoMaximoIdExterno));

        Assert.Equal(69, anonimo.Length);
        Assert.True(anonimo.Length <= Campanha.TamanhoMaximoIdExterno);
    }

    [Fact]
    public void Ids_diferentes_dao_hashes_diferentes()
    {
        Assert.NotEqual(IdExternoAnonimo.De("1234567890123456"), IdExternoAnonimo.De("1234567890123457"));
    }
}
