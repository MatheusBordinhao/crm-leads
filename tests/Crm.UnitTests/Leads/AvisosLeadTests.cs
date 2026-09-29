using Crm.Api.Features.Leads;

namespace Crm.UnitTests.Leads;

public class AvisosLeadTests
{
    [Fact]
    public void Dados_validos_nao_tem_aviso()
    {
        Assert.Empty(AvisosLead.Calcular("maria@empresax.com.br", "+5551999998888", "52998224725"));
    }

    [Fact]
    public void Cada_campo_invalido_tem_o_seu_aviso()
    {
        var avisos = AvisosLead.Calcular("maria@empresax", "123", "52998224726");

        Assert.Equal(
            [
                new AvisoLead("email", AvisosLead.EmailInvalido),
                new AvisoLead("telefone", AvisosLead.TelefoneNaoReconhecido),
                new AvisoLead("cpfCnpj", AvisosLead.DocumentoInvalido),
            ],
            avisos);
    }

    [Fact]
    public void Sem_documento_avisa_e_sem_email_ou_telefone_nao()
    {
        Assert.Equal([new AvisoLead("cpfCnpj", AvisosLead.SemDocumento)], AvisosLead.Calcular(null, "+5551999998888", null));
    }
}
