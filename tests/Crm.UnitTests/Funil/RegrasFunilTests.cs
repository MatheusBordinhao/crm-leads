using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;

namespace Crm.UnitTests.Funil;

public class RegrasFunilTests
{
    private static readonly DateTimeOffset CriadoEm = new(2026, 10, 5, 14, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Agora = CriadoEm.AddHours(3);

    private static readonly Etapa Novo = new() { Id = 1, Nome = "Novo", Ordem = 1, Tipo = TipoEtapa.Aberta };

    private static readonly Etapa EmContato = new() { Id = 2, Nome = "Em contato", Ordem = 2, Tipo = TipoEtapa.Aberta };

    private static readonly Etapa Ganho = new() { Id = 5, Nome = "Ganho", Ordem = 5, Tipo = TipoEtapa.Ganho };

    private static readonly Etapa Perdido = new() { Id = 6, Nome = "Perdido", Ordem = 6, Tipo = TipoEtapa.Perdido };

    private static readonly MotivoPerda SemResposta = new() { Id = 1, Nome = "Sem resposta" };

    private static Lead NovoLead(string? cpfCnpj = null) => new()
    {
        Id = 1024,
        Nome = "Maria Souza",
        CpfCnpj = cpfCnpj,
        EtapaId = Novo.Id,
        CriadoEm = CriadoEm,
        AtualizadoEm = CriadoEm,
    };

    [Fact]
    public void Perdido_exige_um_motivo_ativo()
    {
        var lead = NovoLead();
        var desativado = new MotivoPerda { Id = 2, Nome = "Antigo", Ativo = false };

        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaId", RegrasFunil.MensagemMotivoObrigatorio),
            RegrasFunil.Conferir(lead, Perdido, null, null, null, exigirDocumentoParaGanho: true));
        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaId", RegrasFunil.MensagemMotivoInvalido),
            RegrasFunil.Conferir(lead, Perdido, 99, null, null, exigirDocumentoParaGanho: true));
        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaId", RegrasFunil.MensagemMotivoInvalido),
            RegrasFunil.Conferir(lead, Perdido, desativado.Id, desativado, null, exigirDocumentoParaGanho: true));
        Assert.Null(RegrasFunil.Conferir(lead, Perdido, SemResposta.Id, SemResposta, "Não atende", exigirDocumentoParaGanho: true));
    }

    [Fact]
    public void Num_lead_anonimizado_Perdido_vai_sem_o_detalhe_e_continua_exigindo_o_motivo()
    {
        var anonimizado = NovoLead();
        anonimizado.AnonimizadoEm = CriadoEm.AddHours(1);

        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaDetalhe", Anonimizacao.MensagemSemDetalhePerda),
            RegrasFunil.Conferir(anonimizado, Perdido, SemResposta.Id, SemResposta, "Ligou de outro número", exigirDocumentoParaGanho: true));
        Assert.Null(RegrasFunil.Conferir(anonimizado, Perdido, SemResposta.Id, SemResposta, null, exigirDocumentoParaGanho: true));
        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaId", RegrasFunil.MensagemMotivoObrigatorio),
            RegrasFunil.Conferir(anonimizado, Perdido, null, null, null, exigirDocumentoParaGanho: true));

        // O lead que não foi anonimizado continua aceitando o detalhe.
        Assert.Null(RegrasFunil.Conferir(NovoLead(), Perdido, SemResposta.Id, SemResposta, "Ligou de outro número", exigirDocumentoParaGanho: true));
    }

    [Fact]
    public void Motivo_e_detalhe_so_valem_para_Perdido()
    {
        var lead = NovoLead();

        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaId", RegrasFunil.MensagemMotivoSoEmPerdido),
            RegrasFunil.Conferir(lead, EmContato, SemResposta.Id, SemResposta, null, exigirDocumentoParaGanho: true));
        Assert.Equal(
            new ErroMudancaEtapa("motivoPerdaDetalhe", RegrasFunil.MensagemMotivoSoEmPerdido),
            RegrasFunil.Conferir(lead, EmContato, null, null, "Detalhe", exigirDocumentoParaGanho: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("52998224726")]
    [InlineData("123")]
    public void Ganho_sem_documento_valido_e_recusado_com_a_configuracao_ligada(string? cpfCnpj)
    {
        var lead = NovoLead(cpfCnpj);

        Assert.Equal(
            new ErroMudancaEtapa(null, RegrasFunil.MensagemDocumentoParaGanho),
            RegrasFunil.Conferir(lead, Ganho, null, null, null, exigirDocumentoParaGanho: true));
        Assert.Null(RegrasFunil.Conferir(lead, Ganho, null, null, null, exigirDocumentoParaGanho: false));
    }

    [Theory]
    [InlineData("52998224725")]
    [InlineData("12ABC34501DE35")]
    [InlineData("11222333000181")]
    public void Ganho_com_documento_valido_passa(string cpfCnpj)
    {
        Assert.Null(RegrasFunil.Conferir(NovoLead(cpfCnpj), Ganho, null, null, null, exigirDocumentoParaGanho: true));
    }

    [Fact]
    public void Perdido_grava_a_data_e_o_motivo_no_lead_e_no_historico()
    {
        var lead = NovoLead();
        var usuario = Guid.NewGuid();

        var historico = RegrasFunil.Aplicar(lead, Perdido, Novo.Id, SemResposta, "Não atende", usuario, Agora, contaComoContato: true);

        Assert.Equal(Perdido.Id, lead.EtapaId);
        Assert.Equal(Agora, lead.PerdidoEm);
        Assert.Equal(SemResposta.Id, lead.MotivoPerdaId);
        Assert.Equal("Não atende", lead.MotivoPerdaDetalhe);
        Assert.Null(lead.ConvertidoEm);
        Assert.Equal(Agora, lead.AtualizadoEm);
        Assert.Equal((lead.Id, Novo.Id, Perdido.Id), (historico.LeadId, historico.EtapaAnteriorId, historico.EtapaNovaId));
        Assert.Equal((SemResposta.Id, "Não atende"), (historico.MotivoPerdaId, historico.MotivoPerdaDetalhe));
        Assert.Equal((usuario, Agora), (historico.UsuarioId, historico.OcorridoEm));
    }

    [Fact]
    public void Sair_de_Perdido_limpa_a_data_e_o_motivo_mas_o_historico_guarda()
    {
        var lead = NovoLead();
        var perdido = RegrasFunil.Aplicar(lead, Perdido, Novo.Id, SemResposta, "Não atende", null, Agora, contaComoContato: true);

        var ganho = RegrasFunil.Aplicar(lead, Ganho, Novo.Id, null, null, null, Agora.AddDays(1), contaComoContato: true);

        Assert.Equal(Ganho.Id, lead.EtapaId);
        Assert.Equal(Agora.AddDays(1), lead.ConvertidoEm);
        Assert.Null(lead.PerdidoEm);
        Assert.Null(lead.MotivoPerdaId);
        Assert.Null(lead.MotivoPerdaDetalhe);
        Assert.Equal(SemResposta.Id, perdido.MotivoPerdaId);
        Assert.Equal(Perdido.Id, ganho.EtapaAnteriorId);
    }

    [Fact]
    public void Sair_de_Ganho_limpa_a_conversao()
    {
        var lead = NovoLead("52998224725");
        RegrasFunil.Aplicar(lead, Ganho, Novo.Id, null, null, null, Agora, contaComoContato: true);

        RegrasFunil.Aplicar(lead, EmContato, Novo.Id, null, null, null, Agora.AddDays(1), contaComoContato: true);

        Assert.Null(lead.ConvertidoEm);
        Assert.Equal(EmContato.Id, lead.EtapaId);
    }

    [Fact]
    public void A_primeira_saida_da_etapa_inicial_e_o_primeiro_contato()
    {
        var lead = NovoLead();

        RegrasFunil.Aplicar(lead, EmContato, Novo.Id, null, null, null, Agora, contaComoContato: true);
        RegrasFunil.Aplicar(lead, Novo, Novo.Id, null, null, null, Agora.AddHours(1), contaComoContato: true);
        RegrasFunil.Aplicar(lead, EmContato, Novo.Id, null, null, null, Agora.AddHours(2), contaComoContato: true);

        Assert.Equal(Agora, lead.PrimeiroContatoEm);
    }

    [Fact]
    public void Mudanca_que_nao_conta_como_contato_nao_grava_o_primeiro_contato()
    {
        var lead = NovoLead();

        RegrasFunil.Aplicar(lead, EmContato, Novo.Id, null, null, null, Agora, contaComoContato: false);

        Assert.Null(lead.PrimeiroContatoEm);
    }

    [Fact]
    public void Mudanca_fora_da_etapa_inicial_nao_grava_o_primeiro_contato()
    {
        var lead = NovoLead();
        lead.EtapaId = EmContato.Id;

        RegrasFunil.Aplicar(lead, Perdido, Novo.Id, SemResposta, null, null, Agora, contaComoContato: true);

        Assert.Null(lead.PrimeiroContatoEm);
    }

    [Fact]
    public void Primeiro_contato_e_gravado_uma_vez_e_nunca_antes_da_entrada()
    {
        var lead = NovoLead();
        var antes = NovoLead();

        lead.RegistrarPrimeiroContato(Agora);
        lead.RegistrarPrimeiroContato(Agora.AddHours(1));
        lead.RegistrarPrimeiroContato(Agora.AddHours(-1));
        antes.RegistrarPrimeiroContato(CriadoEm.AddDays(-2));

        Assert.Equal(Agora, lead.PrimeiroContatoEm);
        Assert.Equal(CriadoEm, antes.PrimeiroContatoEm);
    }
}
