using System.Globalization;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Painel;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Demonstracao;

namespace Crm.UnitTests.Demonstracao;

public class MassaDemonstracaoTests
{
    /// <summary>15:30 em Brasília.</summary>
    private static readonly DateTimeOffset Tarde = new(2026, 10, 7, 18, 30, 0, TimeSpan.Zero);

    private static readonly Etapa Novo = new() { Id = 1, Nome = "Novo", Ordem = 1, Tipo = TipoEtapa.Aberta };

    private static readonly Etapa[] Abertas =
    [
        Novo,
        new() { Id = 2, Nome = "Em contato", Ordem = 2, Tipo = TipoEtapa.Aberta },
        new() { Id = 3, Nome = "Qualificado", Ordem = 3, Tipo = TipoEtapa.Aberta },
        new() { Id = 4, Nome = "Proposta enviada", Ordem = 4, Tipo = TipoEtapa.Aberta },
    ];

    private static readonly Etapa Ganho = new() { Id = 5, Nome = "Ganho", Ordem = 5, Tipo = TipoEtapa.Ganho };

    private static readonly Etapa Perdido = new() { Id = 6, Nome = "Perdido", Ordem = 6, Tipo = TipoEtapa.Perdido };

    private static readonly MotivoPerda[] Motivos =
    [
        .. new[] { "Sem resposta", "Sem interesse", "Preço", "Fechou com concorrente", "Fora do perfil", "Contato inválido" }
            .Select((nome, indice) => new MotivoPerda { Id = indice + 1, Nome = nome }),
    ];

    private static readonly Guid Adriana = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private static readonly Guid Pietra = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    private static readonly Dictionary<string, Campanha> Campanhas = MassaDemonstracao.Campanhas
        .Select((c, indice) => (c.Chave, Campanha: new Campanha { Id = indice + 1, Origem = c.Origem, IdExterno = c.IdExterno, Nome = c.Nome }))
        .ToDictionary(c => c.Chave, c => c.Campanha);

    private static IReadOnlyList<LeadDemonstracao> Gerar(DateTimeOffset agora, Etapa[]? abertas = null, Guid[]? atendentes = null) =>
        MassaDemonstracao.Gerar(new ContextoDemonstracao(
            agora, abertas ?? Abertas, Ganho, Perdido, Motivos, atendentes ?? [Adriana, Pietra], Campanhas));

    private static List<(T Chave, int Quantos)> Contar<T>(IEnumerable<LeadDemonstracao> massa, Func<LeadDemonstracao, T> chave)
        where T : notnull =>
        [.. massa.CountBy(chave).Select(p => (p.Key, p.Value))];

    [Fact]
    public void Gera_60_leads_reais_nas_atendentes_em_todas_as_etapas_e_com_os_numeros_das_duas_equilibrados()
    {
        var massa = Gerar(Tarde);

        Assert.Equal(60, massa.Count);
        Assert.All(massa, m => Assert.False(m.Lead.Teste));
        Assert.Equal([(1, 13), (2, 12), (3, 9), (4, 7), (5, 11), (6, 8)], Contar(massa, m => m.Lead.EtapaId).Order());
        Assert.Equal([(Adriana, 31), (Pietra, 29)], Contar(massa, m => m.Lead.ResponsavelId!.Value).OrderBy(p => p.Chave));
        Assert.Equal([(Adriana, 6), (Pietra, 5)], Contar(massa.Where(m => m.Lead.EtapaId == Ganho.Id), m => m.Lead.ResponsavelId!.Value).OrderBy(p => p.Chave));
        Assert.Equal([(Adriana, 4), (Pietra, 4)], Contar(massa.Where(m => m.Lead.EtapaId == Perdido.Id), m => m.Lead.ResponsavelId!.Value).OrderBy(p => p.Chave));
    }

    [Fact]
    public void Leads_vem_das_campanhas_da_Meta_e_do_Google_e_de_outras_origens()
    {
        var massa = Gerar(Tarde);

        Assert.Equal(
            [(Origem.MetaLeadAds, 26), (Origem.GoogleAds, 16), (Origem.Site, 8), (Origem.Whatsapp, 3), (Origem.Telefone, 3), (Origem.Indicacao, 4)],
            Contar(massa, m => m.Lead.Origem).OrderBy(p => p.Chave));
        Assert.Equal(
            [("captacao", 15), ("remarketing", 11), ("pesquisa", 10), ("marca", 6), ("sem campanha", 18)],
            Contar(massa, m => Campanhas.SingleOrDefault(c => c.Value.Id == m.Lead.CampanhaId).Key ?? "sem campanha")
                .OrderBy(p => p.Chave == "sem campanha").ThenByDescending(p => p.Quantos));
    }

    [Theory]
    [InlineData("2026-10-07T18:30:00Z")]
    [InlineData("2026-10-07T03:40:00Z")]
    [InlineData("2026-10-07T11:05:00Z")]
    public void Todas_as_horas_ficam_nos_ultimos_30_dias_e_nunca_no_futuro(string instante)
    {
        var agora = DateTimeOffset.Parse(instante, CultureInfo.InvariantCulture);
        var inicio = HorarioBrasilia.InicioDoDia(HorarioBrasilia.Dia(agora).AddDays(-29));

        foreach (var (lead, entradas, interacoes, historico) in Gerar(agora))
        {
            Assert.InRange(lead.CriadoEm, inicio, agora);
            Assert.InRange(lead.AtualizadoEm, lead.CriadoEm, agora);
            var horas = entradas.SelectMany(e => new[] { e.RecebidoEm, e.ProcessadoEm })
                .Concat(interacoes.SelectMany(i => new[] { i.OcorridoEm, i.CriadoEm }))
                .Concat(historico.Select(h => h.OcorridoEm))
                .Concat(new[] { lead.PrimeiroContatoEm, lead.ConvertidoEm, lead.PerdidoEm }.OfType<DateTimeOffset>());
            Assert.All(horas, hora => Assert.InRange(hora, lead.CriadoEm, agora));
        }
    }

    /// <summary>
    /// O comando pode rodar a qualquer hora, inclusive de manhã cedo: em nenhuma, um lead, uma entrada, um contato ou uma
    /// mudança de etapa fica no futuro, e os contatos e as mudanças ficam entre 8h e 20h em Brasília.
    /// </summary>
    [Fact]
    public void Rodando_a_cada_10_minutos_de_dois_dias_nada_fica_no_futuro_nem_fora_do_horario_comercial()
    {
        static DateTimeOffset EmBrasilia(DateTimeOffset momento) => TimeZoneInfo.ConvertTime(momento, HorarioBrasilia.Fuso);

        var problemas = new List<string>();
        for (var agora = new DateTimeOffset(2026, 10, 8, 3, 0, 0, TimeSpan.Zero); agora < new DateTimeOffset(2026, 10, 10, 3, 0, 0, TimeSpan.Zero); agora = agora.AddMinutes(10))
        {
            var quando = EmBrasilia(agora).ToString("dd/MM HH:mm", CultureInfo.InvariantCulture);
            foreach (var (lead, entradas, interacoes, historico) in Gerar(agora))
            {
                var horas = entradas.SelectMany(e => new[] { e.RecebidoEm, e.ProcessadoEm })
                    .Concat(interacoes.SelectMany(i => new[] { i.OcorridoEm, i.CriadoEm }))
                    .Concat(historico.Select(h => h.OcorridoEm))
                    .Concat(new[] { lead.CriadoEm, lead.AtualizadoEm, lead.PrimeiroContatoEm, lead.ConvertidoEm, lead.PerdidoEm }.OfType<DateTimeOffset>());
                problemas.AddRange(horas.Where(h => h > agora)
                    .Select(h => $"rodando às {quando}, {lead.Nome} tem uma hora no futuro: {EmBrasilia(h):dd/MM HH:mm}"));

                var atendimento = interacoes.Select(i => i.OcorridoEm).Concat(historico.Select(h => h.OcorridoEm));
                problemas.AddRange(atendimento.Where(h => EmBrasilia(h).Hour is < 8 or >= 20)
                    .Select(h => $"rodando às {quando}, {lead.Nome} tem atendimento fora do horário: {EmBrasilia(h):dd/MM HH:mm}"));
            }
        }

        Assert.Empty(problemas);
    }

    [Fact]
    public void Tres_leads_sao_de_hoje_e_o_mais_novo_entrou_ha_20_minutos_sem_contato()
    {
        var massa = Gerar(Tarde);

        var deHoje = massa.Where(m => HorarioBrasilia.Dia(m.Lead.CriadoEm) == HorarioBrasilia.Dia(Tarde)).ToList();
        var maisNovo = massa.MaxBy(m => m.Lead.CriadoEm)!.Lead;

        Assert.Equal(3, deHoje.Count);
        Assert.Equal(Tarde.AddMinutes(-20), maisNovo.CriadoEm);
        Assert.Null(maisNovo.PrimeiroContatoEm);
    }

    [Fact]
    public void Historico_anda_em_ordem_pelas_etapas_e_termina_na_etapa_do_lead()
    {
        foreach (var (lead, _, _, historico) in Gerar(Tarde))
        {
            var etapa = Novo.Id;
            var antes = lead.CriadoEm;
            foreach (var mudanca in historico)
            {
                Assert.Equal(etapa, mudanca.EtapaAnteriorId);
                Assert.True(mudanca.OcorridoEm > antes, $"A mudança do lead {lead.Nome} veio fora de ordem.");
                Assert.Equal(lead.ResponsavelId, mudanca.UsuarioId);
                etapa = mudanca.EtapaNovaId;
                antes = mudanca.OcorridoEm;
            }

            Assert.Equal(lead.EtapaId, etapa);
        }
    }

    [Fact]
    public void Ganho_tem_empresa_e_CNPJ_valido_e_perdido_tem_o_motivo_da_ultima_mudanca()
    {
        var massa = Gerar(Tarde);

        foreach (var (lead, _, _, historico) in massa)
        {
            if (lead.EtapaId == Ganho.Id)
            {
                Assert.True(Documentos.Valido(lead.CpfCnpj));
                Assert.StartsWith("DEMO", lead.CpfCnpj, StringComparison.Ordinal);
                Assert.NotNull(lead.Empresa);
                Assert.Equal(historico[^1].OcorridoEm, lead.ConvertidoEm);
                Assert.Null(lead.PerdidoEm);
            }
            else if (lead.EtapaId == Perdido.Id)
            {
                Assert.Equal(historico[^1].MotivoPerdaId, lead.MotivoPerdaId);
                Assert.Equal(historico[^1].OcorridoEm, lead.PerdidoEm);
                Assert.Null(lead.ConvertidoEm);
            }
            else
            {
                Assert.Null(lead.ConvertidoEm);
                Assert.Null(lead.PerdidoEm);
                Assert.Null(lead.MotivoPerdaId);
            }
        }

        Assert.Equal(
            [("Sem resposta", 3), ("Preço", 2), ("Fechou com concorrente", 1), ("Fora do perfil", 1), ("Sem interesse", 1)],
            Contar(massa.Where(m => m.Lead.MotivoPerdaId is not null), m => Motivos.Single(x => x.Id == m.Lead.MotivoPerdaId).Nome)
                .OrderByDescending(p => p.Quantos).ThenBy(p => p.Chave, StringComparer.Ordinal));
    }

    [Fact]
    public void Primeiro_contato_vem_da_primeira_interacao_com_mediana_de_25_minutos_e_oito_estao_sem_contato()
    {
        var massa = Gerar(Tarde);

        var semContato = massa.Where(m => m.Lead.PrimeiroContatoEm is null).ToList();
        Assert.Equal(8, semContato.Count);
        Assert.All(semContato, m =>
        {
            Assert.Equal(Novo.Id, m.Lead.EtapaId);
            Assert.Empty(m.Interacoes);
            Assert.Empty(m.Historico);
        });

        foreach (var (lead, _, interacoes, historico) in massa.Except(semContato))
        {
            var primeira = interacoes.Where(i => Interacao.EhContato(i.Tipo)).MinBy(i => i.OcorridoEm)!;
            Assert.Equal(primeira.OcorridoEm, lead.PrimeiroContatoEm);
            Assert.True(historico.Count == 0 || historico[0].OcorridoEm > lead.PrimeiroContatoEm);
        }

        var minutos = massa.Except(semContato).Select(m => (m.Lead.PrimeiroContatoEm!.Value - m.Lead.CriadoEm).TotalMinutes);
        Assert.Equal(25.0, MetricasPainel.Mediana(minutos));
    }

    [Fact]
    public void Cada_lead_tem_a_entrada_que_o_criou_e_cinco_pessoas_voltaram_pela_pesquisa_do_Google()
    {
        var massa = Gerar(Tarde);

        Assert.All(massa, m =>
        {
            var criacao = Assert.Single(m.Entradas, e => e.Situacao == SituacaoEntrada.Criado);
            Assert.Equal((m.Lead.Origem, m.Lead.CampanhaId, m.Lead.CriadoEm), (criacao.Origem, criacao.CampanhaId, criacao.ProcessadoEm));
        });
        var voltas = massa.SelectMany(m => m.Entradas).Where(e => e.Situacao == SituacaoEntrada.Vinculado).ToList();
        Assert.Equal(MassaDemonstracao.Voltas, voltas.Count);
        Assert.All(voltas, e => Assert.Equal((Origem.GoogleAds, Campanhas["pesquisa"].Id), (e.Origem, e.CampanhaId)));
        Assert.All(
            massa.SelectMany(m => m.Entradas).Where(e => e.IdExterno is not null).GroupBy(e => (e.Origem, e.IdExterno)),
            grupo => Assert.Single(grupo));
    }

    [Fact]
    public void E_mails_e_telefones_ficticios_passam_na_normalizacao_e_nao_se_repetem()
    {
        var massa = Gerar(Tarde);

        Assert.All(massa, m =>
        {
            Assert.EndsWith("@example.com", m.Lead.Email, StringComparison.Ordinal);
            Assert.Equal(m.Lead.Email, Emails.Normalizar(m.Lead.Email));
            Assert.True(Emails.Valido(m.Lead.Email));
            Assert.Equal(m.Lead.Telefone, Telefones.Normalizar(m.Lead.Telefone));
            Assert.True(Telefones.Valido(m.Lead.Telefone));
        });
        Assert.Equal(60, massa.Select(m => m.Lead.Email).Distinct().Count());
        Assert.Equal(60, massa.Select(m => m.Lead.Telefone).Distinct().Count());
        Assert.Equal("joao.vitor.pacheco@example.com", MassaDemonstracao.Email("João Vitor Pacheco"));
    }

    [Fact]
    public void Duas_rodadas_dao_a_mesma_massa()
    {
        static object Resumo(LeadDemonstracao m) => (
            m.Lead.Nome, m.Lead.EtapaId, m.Lead.ResponsavelId, m.Lead.CampanhaId, m.Lead.CriadoEm, m.Lead.PrimeiroContatoEm,
            m.Lead.CpfCnpj, m.Entradas.Count, m.Interacoes.Count, m.Historico.Count);

        Assert.Equal(Gerar(Tarde).Select(Resumo), Gerar(Tarde).Select(Resumo));
    }

    [Fact]
    public void Funciona_com_um_funil_so_com_a_etapa_inicial_e_uma_atendente()
    {
        var massa = Gerar(Tarde, abertas: [Novo], atendentes: [Adriana]);

        Assert.Equal(60, massa.Count);
        Assert.All(massa, m => Assert.Equal(Adriana, m.Lead.ResponsavelId));
        Assert.Equal([(1, 41), (5, 11), (6, 8)], Contar(massa, m => m.Lead.EtapaId).Order());
        Assert.All(massa, m => Assert.All(m.Historico, h => Assert.Equal(Novo.Id, h.EtapaAnteriorId)));
    }

    [Fact]
    public void O_comando_e_reconhecido_entre_os_argumentos()
    {
        Assert.True(ComandoDemonstracao.Pedido(["preparar-demonstracao"]));
        Assert.True(ComandoDemonstracao.Pedido(["--N8n:WebhookUrl=", "preparar-demonstracao"]));
        Assert.False(ComandoDemonstracao.Pedido([]));
        Assert.False(ComandoDemonstracao.Pedido(["--preparar-demonstracao"]));
    }
}
