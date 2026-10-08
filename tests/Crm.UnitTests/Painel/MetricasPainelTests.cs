using System.Globalization;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Painel;
using Crm.Api.Infra.Datas;

namespace Crm.UnitTests.Painel;

public class MetricasPainelTests
{
    private static readonly DateTimeOffset CriadoEm = new(2026, 9, 10, 15, 0, 0, TimeSpan.Zero);

    private static LeadDaCoorte Lead(TipoEtapa tipo, int? minutosAteOContato) => new(
        EtapaId: (int)tipo,
        TipoEtapa: tipo,
        ResponsavelId: null,
        Origem: Origem.MetaLeadAds,
        CampanhaId: null,
        CriadoEm: CriadoEm,
        PrimeiroContatoEm: minutosAteOContato is { } minutos ? CriadoEm.AddMinutes(minutos) : null);

    [Fact]
    public void Mediana_e_o_valor_do_meio_e_com_quantidade_par_a_media_dos_dois_do_meio()
    {
        Assert.Null(MetricasPainel.Mediana([]));
        Assert.Equal(5.0, MetricasPainel.Mediana([5]));
        Assert.Equal(30.0, MetricasPainel.Mediana([90, 10, 30]));
        Assert.Equal(45.0, MetricasPainel.Mediana([60, 10, 90, 30]));
        Assert.Equal(1.5, MetricasPainel.Mediana([2, 1]));
    }

    [Fact]
    public void Taxa_e_a_parte_do_total_e_nao_existe_sem_total()
    {
        Assert.Null(MetricasPainel.Taxa(0, 0));
        Assert.Equal(0.0, MetricasPainel.Taxa(0, 3));
        Assert.Equal(0.25, MetricasPainel.Taxa(1, 4));
    }

    [Fact]
    public void Resumo_conta_os_leads_pelo_tipo_da_etapa_e_o_contato_em_minutos()
    {
        var resumo = MetricasPainel.Resumir(
        [
            Lead(TipoEtapa.Ganho, 10),
            Lead(TipoEtapa.Aberta, 30),
            Lead(TipoEtapa.Perdido, 60),
            Lead(TipoEtapa.Aberta, null),
        ]);

        Assert.Equal(
            new ResumoCoorte(
                LeadsNovos: 4, EmAndamento: 2, Ganhos: 1, Perdidos: 1, TaxaConversao: 0.25, MedianaPrimeiroContatoMinutos: 30, ComContato: 3, SemContato: 1),
            resumo);
    }

    [Fact]
    public void Resumo_sem_leads_nao_tem_taxa_nem_mediana()
    {
        Assert.Equal(new ResumoCoorte(0, 0, 0, 0, null, null, 0, 0), MetricasPainel.Resumir([]));
    }

    [Fact]
    public void Periodo_padrao_sao_os_30_dias_que_terminam_hoje_ou_no_fim_escolhido()
    {
        var hoje = new DateOnly(2026, 10, 6);

        Assert.Equal(new PeriodoPainel(new(2026, 9, 7), hoje), PeriodoPainel.Resolver(null, null, hoje));
        Assert.Equal(new PeriodoPainel(new(2026, 9, 1), new(2026, 9, 30)), PeriodoPainel.Resolver(null, new(2026, 9, 30), hoje));
        Assert.Equal(new PeriodoPainel(new(2026, 9, 15), hoje), PeriodoPainel.Resolver(new(2026, 9, 15), null, hoje));
        Assert.Equal(new PeriodoPainel(new(2026, 8, 1), new(2026, 8, 31)), PeriodoPainel.Resolver(new(2026, 8, 1), new(2026, 8, 31), hoje));
    }

    [Theory]
    [InlineData("2026-10-05")] // segunda
    [InlineData("2026-10-06")]
    [InlineData("2026-10-07")] // quarta
    [InlineData("2026-10-08")]
    [InlineData("2026-10-09")]
    [InlineData("2026-10-10")]
    [InlineData("2026-10-11")] // domingo
    public void Semana_anterior_vai_de_segunda_a_domingo_da_semana_passada_qualquer_que_seja_o_dia(string dia)
    {
        var hoje = DateOnly.Parse(dia, CultureInfo.InvariantCulture);

        Assert.Equal(new PeriodoPainel(new(2026, 9, 28), new(2026, 10, 4)), PeriodoPainel.SemanaAnterior(hoje));
    }

    [Fact]
    public void Num_domingo_a_semana_que_termina_hoje_ainda_nao_conta()
    {
        Assert.Equal(new PeriodoPainel(new(2026, 9, 21), new(2026, 9, 27)), PeriodoPainel.SemanaAnterior(new(2026, 10, 4)));
    }

    [Fact]
    public void Semana_anterior_conta_pelo_dia_em_Brasilia_e_atravessa_o_ano()
    {
        // 02:30 UTC de segunda ainda é domingo, 23:30, em Brasília; às 03:00 UTC, já é segunda.
        var domingoEmBrasilia = HorarioBrasilia.Dia(new DateTimeOffset(2026, 10, 5, 2, 30, 0, TimeSpan.Zero));
        var segundaEmBrasilia = HorarioBrasilia.Dia(new DateTimeOffset(2026, 10, 5, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new PeriodoPainel(new(2026, 9, 21), new(2026, 9, 27)), PeriodoPainel.SemanaAnterior(domingoEmBrasilia));
        Assert.Equal(new PeriodoPainel(new(2026, 9, 28), new(2026, 10, 4)), PeriodoPainel.SemanaAnterior(segundaEmBrasilia));
        Assert.Equal(new PeriodoPainel(new(2026, 12, 28), new(2027, 1, 3)), PeriodoPainel.SemanaAnterior(new(2027, 1, 6)));
    }

    [Fact]
    public void Periodo_vai_da_meia_noite_do_primeiro_dia_ate_a_do_dia_seguinte_ao_ultimo_em_Brasilia()
    {
        var setembro = new PeriodoPainel(new(2026, 9, 1), new(2026, 9, 30));

        Assert.Equal(new DateTimeOffset(2026, 9, 1, 3, 0, 0, TimeSpan.Zero), setembro.Inicio);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero), setembro.Fim);
    }
}
