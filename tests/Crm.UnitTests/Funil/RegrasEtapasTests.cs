using Crm.Api.Features.Funil;

namespace Crm.UnitTests.Funil;

public class RegrasEtapasTests
{
    private static Etapa NovaEtapa(int id, TipoEtapa tipo = TipoEtapa.Aberta, bool ativa = true) =>
        new() { Id = id, Nome = $"Etapa {id}", Ordem = id, Tipo = tipo, Ativa = ativa };

    [Fact]
    public void Ganho_e_Perdido_nao_sao_desativadas()
    {
        Assert.Equal(RegrasEtapas.MensagemFechamentoFixo, RegrasEtapas.ConferirDesativacao(NovaEtapa(5, TipoEtapa.Ganho), 4));
        Assert.Equal(RegrasEtapas.MensagemFechamentoFixo, RegrasEtapas.ConferirDesativacao(NovaEtapa(6, TipoEtapa.Perdido), 4));
    }

    [Fact]
    public void A_ultima_etapa_aberta_ativa_nao_e_desativada()
    {
        Assert.Equal(RegrasEtapas.MensagemUltimaAberta, RegrasEtapas.ConferirDesativacao(NovaEtapa(1), abertasAtivas: 1));
        Assert.Null(RegrasEtapas.ConferirDesativacao(NovaEtapa(1), abertasAtivas: 2));
        Assert.Null(RegrasEtapas.ConferirDesativacao(NovaEtapa(7, ativa: false), abertasAtivas: 1));
    }

    [Fact]
    public void Numera_as_abertas_na_ordem_pedida_e_deixa_Ganho_e_Perdido_no_fim()
    {
        var abertas = new[] { NovaEtapa(3), NovaEtapa(1), NovaEtapa(2, ativa: false) };
        var perdido = NovaEtapa(6, TipoEtapa.Perdido);
        var ganho = NovaEtapa(5, TipoEtapa.Ganho);
        var ganhoAntigo = NovaEtapa(4, TipoEtapa.Ganho, ativa: false);

        RegrasEtapas.Numerar(abertas, [perdido, ganhoAntigo, ganho]);

        Assert.Equal([1, 2, 3], abertas.Select(e => e.Ordem));
        Assert.Equal((4, 5, 6), (ganho.Ordem, perdido.Ordem, ganhoAntigo.Ordem));
    }

    [Fact]
    public void Mensagem_dos_leads_na_etapa_concorda_com_a_quantidade()
    {
        Assert.Equal("A etapa tem 1 lead. Escolha para qual etapa ele vai antes de desativar.", RegrasEtapas.MensagemTemLeads(1));
        Assert.Equal("A etapa tem 3 leads. Escolha para qual etapa eles vão antes de desativar.", RegrasEtapas.MensagemTemLeads(3));
    }

    [Theory]
    [InlineData("   ", "Informe o nome.")]
    [InlineData("123456789012345678901234567890123456789012345678901", "O nome pode ter até 50 caracteres.")]
    public void Nome_da_etapa_e_obrigatorio_e_tem_limite(string nome, string erro)
    {
        var falha = Assert.Single(new CriarEtapaRequestValidator().Validate(new CriarEtapaRequest(nome)).Errors);

        Assert.Equal(("Nome", erro), (falha.PropertyName, falha.ErrorMessage));
    }

    [Fact]
    public void Nome_com_espacos_a_mais_conta_so_o_texto()
    {
        Assert.True(new RenomearEtapaRequestValidator().Validate(new RenomearEtapaRequest($"  {new string('a', 50)}  ")).IsValid);
    }

    [Fact]
    public void Ordem_precisa_de_ids_sem_repeticao()
    {
        Assert.False(new ReordenarEtapasRequestValidator().Validate(new ReordenarEtapasRequest([1, 2, 1])).IsValid);
        Assert.False(new ReordenarEtapasRequestValidator().Validate(new ReordenarEtapasRequest([])).IsValid);
        Assert.True(new ReordenarEtapasRequestValidator().Validate(new ReordenarEtapasRequest([2, 1])).IsValid);
    }
}
