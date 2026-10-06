using Crm.Api.Infra.TempoReal;

namespace Crm.UnitTests.Infra;

public class TempoRealTests
{
    private static readonly Guid Adriana = Guid.NewGuid();
    private static readonly Guid Pietra = Guid.NewGuid();

    private static MudancasTempoReal Leads(params long[] ids) => new(ids, [], [], false);

    private static EnvioTempoReal Montar(MudancasTempoReal[] lote, params (long Id, Guid? Responsavel, bool Teste)[] leads) =>
        EnvioTempoReal.Montar(lote, leads.ToDictionary(l => l.Id, l => new LeadNoEnvio(l.Responsavel, l.Teste)));

    /// <summary>Nenhum dado pessoal passa pelo hub: os avisos só levam ids, e a tela busca os dados na API.</summary>
    [Fact]
    public void Os_avisos_do_hub_so_levam_ids()
    {
        var parametros = typeof(IClienteTempoReal).GetMethods().SelectMany(m => m.GetParameters()).ToList();

        Assert.NotEmpty(parametros);
        Assert.All(parametros, p => Assert.Contains(p.ParameterType, new[] { typeof(long), typeof(long[]) }));
    }

    [Fact]
    public void Os_admins_recebem_todos_os_leads_inclusive_os_de_teste_e_os_excluidos()
    {
        var envio = Montar([Leads(3, 1), Leads(2, 1)], (1, Adriana, false), (2, null, true));

        Assert.Equal([1, 2, 3], envio.LeadsParaAdmins);
    }

    [Fact]
    public void Cada_responsavel_recebe_so_os_proprios_leads_que_nao_sao_de_teste()
    {
        var envio = Montar(
            [Leads(1, 2, 3, 4)],
            (1, Adriana, false),
            (2, Pietra, false),
            (3, null, false),
            (4, null, true));

        Assert.Equal(2, envio.LeadsPorUsuario.Count);
        Assert.Equal([1], envio.LeadsPorUsuario[Adriana]);
        Assert.Equal([2], envio.LeadsPorUsuario[Pietra]);
    }

    [Fact]
    public void A_responsavel_e_a_de_agora_e_nao_a_da_hora_da_mudanca()
    {
        // O lead mudou com a Adriana, mas, na hora do envio, já é da Pietra.
        var envio = Montar([Leads(1)], (1, Pietra, false));

        Assert.False(envio.LeadsPorUsuario.ContainsKey(Adriana));
        Assert.Equal([1], envio.LeadsPorUsuario[Pietra]);
    }

    [Fact]
    public void Quem_perde_um_lead_numa_troca_recebe_um_aviso_sem_o_id_dele()
    {
        var troca = new MudancasTempoReal([1], [new TrocaDeResponsavel(1, Adriana)], [], false);

        var envio = Montar([troca], (1, Pietra, false));

        Assert.Equal([Adriana], envio.RecarregarTudo);
        Assert.False(envio.LeadsPorUsuario.ContainsKey(Adriana));
        Assert.Equal([1], envio.LeadsPorUsuario[Pietra]);
    }

    [Fact]
    public void Quem_recebe_o_lead_de_volta_no_mesmo_lote_recebe_o_id_e_nao_o_aviso_sem_ids()
    {
        var idaEVolta = new MudancasTempoReal(
            [1], [new TrocaDeResponsavel(1, Adriana), new TrocaDeResponsavel(1, Pietra)], [], false);

        var envio = Montar([idaEVolta], (1, Adriana, false));

        Assert.Equal([Pietra], envio.RecarregarTudo);
        Assert.Equal([1], envio.LeadsPorUsuario[Adriana]);
    }

    [Fact]
    public void Cada_notificacao_vai_so_para_o_dono_e_o_funil_para_todos()
    {
        var lote = new[]
        {
            new MudancasTempoReal([], [], [new NotificacaoGravada(7, Adriana)], false),
            new MudancasTempoReal([], [], [new NotificacaoGravada(8, Pietra)], true),
        };

        var envio = Montar(lote);

        Assert.Equal([new NotificacaoGravada(7, Adriana), new NotificacaoGravada(8, Pietra)], envio.Notificacoes);
        Assert.True(envio.Funil);
        Assert.Empty(envio.LeadsParaAdmins);
    }

    [Fact]
    public void Os_SaveChanges_de_uma_transacao_viram_um_aviso_so()
    {
        var juntas = MudancasTempoReal.Juntar(
        [
            new MudancasTempoReal([1], [], [new NotificacaoGravada(7, Adriana)], false),
            new MudancasTempoReal([1, 2], [new TrocaDeResponsavel(2, Pietra)], [], true),
        ]);

        Assert.Equal([1, 2], juntas.Leads);
        Assert.Equal([new TrocaDeResponsavel(2, Pietra)], juntas.Trocas);
        Assert.Equal([new NotificacaoGravada(7, Adriana)], juntas.Notificacoes);
        Assert.True(juntas.Funil);
    }
}
