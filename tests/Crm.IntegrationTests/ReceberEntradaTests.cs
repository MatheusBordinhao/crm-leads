using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>O serviço de entrada da seção 3.2, chamado direto. A entrada pela API está em IntegracaoLeadsTests.</summary>
public class ReceberEntradaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<List<EntradaRecebida>> EntradasDoLeadAsync(long leadId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().EntradasRecebidas
            .AsNoTracking()
            .Where(e => e.LeadId == leadId)
            .OrderBy(e => e.Id)
            .ToListAsync(Ct);
    }

    [Fact]
    public async Task Pessoa_nova_vira_lead_na_etapa_inicial_com_os_dados_da_entrada()
    {
        var entrada = LeadsDeTeste.Entrada() with
        {
            Nome = "  Maria   Souza ",
            Email = $" {LeadsDeTeste.EmailUnico().ToUpperInvariant()} ",
            Telefone = "(51) 99999-8888",
            Uf = "Rio Grande do Sul",
            Empresa = "Empresa X",
            Plataforma = "Instagram",
            Campanha = new ReferenciaExterna(LeadsDeTeste.IdExternoUnico(), "Outubro | Serviço Y"),
            Conjunto = new ReferenciaExterna("120210000000002", "RS | 25-55"),
            Utm = new Utm(Source: "instagram", Medium: "paid"),
            RespostasExtras = new Dictionary<string, JsonElement>
            {
                ["Qual serviço você procura?"] = JsonSerializer.SerializeToElement("Serviço Y"),
            },
            PayloadBruto = JsonDocument.Parse("""{ "qualquer": "coisa" }"""),
        };

        var resultado = await fabrica.ReceberAsync(entrada);

        Assert.Equal(ResultadoEntrada.Criado, resultado.Resultado);
        var lead = await fabrica.BuscarLeadAsync(resultado.LeadId);
        Assert.Equal("Maria Souza", lead.Nome);
        Assert.Equal(entrada.Email.Trim().ToLowerInvariant(), lead.Email);
        Assert.Equal("+5551999998888", lead.Telefone);
        Assert.Equal("RS", lead.Uf);
        Assert.Equal("Novo", lead.Etapa.Nome);
        Assert.Equal(Origem.MetaLeadAds, lead.Origem);
        Assert.Equal("instagram", lead.Plataforma);
        Assert.Equal("Outubro | Serviço Y", lead.Campanha!.Nome);
        Assert.Equal("instagram", lead.UtmSource);
        Assert.Equal("Serviço Y", lead.RespostasExtras!.RootElement.GetProperty("Qual serviço você procura?").GetString());
        Assert.Null(lead.ResponsavelId);
        Assert.False(lead.Teste);

        var registro = Assert.Single(await EntradasDoLeadAsync(lead.Id));
        Assert.Equal(SituacaoEntrada.Criado, registro.Situacao);
        Assert.Equal(entrada.IdExterno, registro.IdExterno);
        Assert.Equal(lead.CampanhaId, registro.CampanhaId);
        Assert.Equal("RS | 25-55", registro.Conjunto!.RootElement.GetProperty("nome").GetString());
        Assert.Equal("coisa", registro.PayloadBruto!.RootElement.GetProperty("qualquer").GetString());
    }

    [Fact]
    public async Task Mesma_origem_e_id_externo_responde_ja_recebido_sem_gravar_nada()
    {
        var entrada = LeadsDeTeste.Entrada();
        var primeira = await fabrica.ReceberAsync(entrada);

        var segunda = await fabrica.ReceberAsync(entrada with { Email = LeadsDeTeste.EmailUnico(), Nome = "Outro nome" });

        Assert.Equal(new EntradaProcessada(primeira.LeadId, ResultadoEntrada.JaRecebido), segunda);
        Assert.Single(await EntradasDoLeadAsync(primeira.LeadId));
        Assert.Equal("Maria Souza", (await fabrica.BuscarLeadAsync(primeira.LeadId)).Nome);
    }

    [Fact]
    public async Task Mesmo_email_de_outra_origem_vincula_e_so_preenche_os_campos_vazios()
    {
        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Telefone = null });
        var lead = await fabrica.BuscarLeadAsync(primeira.LeadId);
        var telefone = LeadsDeTeste.TelefoneUnico();

        var segunda = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds, nome: "Outro Nome") with
        {
            Email = lead.Email!.ToUpperInvariant(),
            Telefone = telefone,
            Empresa = "Empresa Nova",
        });

        Assert.Equal(new EntradaProcessada(primeira.LeadId, ResultadoEntrada.Vinculado), segunda);
        var vinculado = await fabrica.BuscarLeadAsync(primeira.LeadId);
        Assert.Equal("Maria Souza", vinculado.Nome);
        Assert.Equal(Origem.MetaLeadAds, vinculado.Origem);
        Assert.Equal(telefone, vinculado.Telefone);
        Assert.Equal("Empresa Nova", vinculado.Empresa);
        Assert.Equal(
            [(Origem.MetaLeadAds, SituacaoEntrada.Criado), (Origem.GoogleAds, SituacaoEntrada.Vinculado)],
            (await EntradasDoLeadAsync(primeira.LeadId)).Select(e => (e.Origem, e.Situacao)));
    }

    [Fact]
    public async Task Lead_perdido_que_volta_a_entrar_e_reaberto_na_etapa_inicial_com_historico()
    {
        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada());
        await fabrica.MoverParaAsync(primeira.LeadId, TipoEtapa.Perdido);
        var perdido = await fabrica.BuscarLeadAsync(primeira.LeadId);

        var segunda = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with { Email = perdido.Email });

        Assert.Equal(ResultadoEntrada.Vinculado, segunda.Resultado);
        var reaberto = await fabrica.BuscarLeadAsync(primeira.LeadId);
        Assert.Equal("Novo", reaberto.Etapa.Nome);
        Assert.Null(reaberto.PerdidoEm);
        Assert.Null(reaberto.MotivoPerdaId);
        Assert.Null(reaberto.MotivoPerdaDetalhe);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var historico = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().HistoricoEtapas
            .Where(h => h.LeadId == primeira.LeadId)
            .ToListAsync(Ct);
        var mudanca = Assert.Single(historico);
        Assert.Equal(perdido.EtapaId, mudanca.EtapaAnteriorId);
        Assert.Equal(reaberto.EtapaId, mudanca.EtapaNovaId);
        Assert.Null(mudanca.UsuarioId);
    }

    [Fact]
    public async Task Lead_ganho_que_volta_a_entrar_continua_em_ganho()
    {
        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada());
        await fabrica.MoverParaAsync(primeira.LeadId, TipoEtapa.Ganho);
        var ganho = await fabrica.BuscarLeadAsync(primeira.LeadId);

        var segunda = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.Site) with { Email = ganho.Email });

        Assert.Equal(ResultadoEntrada.Vinculado, segunda.Resultado);
        var depois = await fabrica.BuscarLeadAsync(primeira.LeadId);
        Assert.Equal(TipoEtapa.Ganho, depois.Etapa.Tipo);
        Assert.NotNull(depois.ConvertidoEm);
    }

    [Fact]
    public async Task Documento_valido_e_procurado_antes_do_email()
    {
        var cpf = LeadsDeTeste.CpfUnico();
        var comDocumento = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { CpfCnpj = cpf });
        var outro = await fabrica.ReceberAsync(LeadsDeTeste.Entrada());
        var emailDoOutro = (await fabrica.BuscarLeadAsync(outro.LeadId)).Email;

        var resultado = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { CpfCnpj = cpf, Email = emailDoOutro });

        Assert.Equal(new EntradaProcessada(comDocumento.LeadId, ResultadoEntrada.Vinculado), resultado);
    }

    [Fact]
    public async Task Telefone_valido_encontra_a_pessoa_em_qualquer_formato()
    {
        var telefone = LeadsDeTeste.TelefoneUnico();
        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = null, Telefone = telefone });

        var nacional = $"({telefone[3..5]}) {telefone[5..10]}-{telefone[10..]}";
        var resultado = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = null, Telefone = nacional });

        Assert.Equal(new EntradaProcessada(primeira.LeadId, ResultadoEntrada.Vinculado), resultado);
    }

    [Fact]
    public async Task Valores_invalidos_nao_juntam_pessoas_diferentes()
    {
        var emailInvalido = $"invalido-{Guid.NewGuid():N}@sem-dominio";
        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = emailInvalido, Telefone = "123", CpfCnpj = "000" });

        var segunda = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = emailInvalido, Telefone = "123", CpfCnpj = "000" });

        Assert.Equal(ResultadoEntrada.Criado, segunda.Resultado);
        Assert.NotEqual(primeira.LeadId, segunda.LeadId);
        var lead = await fabrica.BuscarLeadAsync(segunda.LeadId);
        Assert.Equal(emailInvalido, lead.Email);
        Assert.Equal("123", lead.Telefone);
        Assert.Equal("000", lead.CpfCnpj);
    }

    [Fact]
    public async Task Entrada_de_teste_so_vincula_a_lead_de_teste()
    {
        var real = await fabrica.ReceberAsync(LeadsDeTeste.Entrada());
        var email = (await fabrica.BuscarLeadAsync(real.LeadId)).Email;

        var teste = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = email, Teste = true });
        var outroTeste = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with { Email = email, Teste = true });
        var outroReal = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.Site) with { Email = email });

        Assert.Equal(ResultadoEntrada.Criado, teste.Resultado);
        Assert.NotEqual(real.LeadId, teste.LeadId);
        Assert.Equal(new EntradaProcessada(teste.LeadId, ResultadoEntrada.Vinculado), outroTeste);
        Assert.Equal(new EntradaProcessada(real.LeadId, ResultadoEntrada.Vinculado), outroReal);
        Assert.True((await fabrica.BuscarLeadAsync(teste.LeadId)).Teste);
    }

    [Fact]
    public async Task Entrada_real_nunca_vincula_a_lead_de_teste()
    {
        // Só existe o lead de teste: sem o filtro, a entrada real seria vinculada a ele.
        var cpf = LeadsDeTeste.CpfUnico();
        var telefone = LeadsDeTeste.TelefoneUnico();
        var teste = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { CpfCnpj = cpf, Telefone = telefone, Teste = true });
        var email = (await fabrica.BuscarLeadAsync(teste.LeadId)).Email;

        var real = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with
        {
            CpfCnpj = cpf,
            Email = email,
            Telefone = telefone,
        });

        Assert.Equal(ResultadoEntrada.Criado, real.Resultado);
        Assert.NotEqual(teste.LeadId, real.LeadId);
        Assert.False((await fabrica.BuscarLeadAsync(real.LeadId)).Teste);
        Assert.Single(await EntradasDoLeadAsync(teste.LeadId));
    }

    [Fact]
    public async Task Lead_de_teste_fica_sem_responsavel()
    {
        var atendente = await fabrica.CriarUsuarioAsync();

        var resultado = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Teste = true, ResponsavelId = atendente.Id });

        Assert.Null((await fabrica.BuscarLeadAsync(resultado.LeadId)).ResponsavelId);
    }

    [Fact]
    public async Task Campanha_sem_nome_ganha_nome_provisorio_que_o_nome_real_substitui()
    {
        var idCampanha = LeadsDeTeste.IdExternoUnico();

        var primeira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with { Campanha = new ReferenciaExterna(idCampanha) });
        var provisoria = (await fabrica.BuscarLeadAsync(primeira.LeadId)).Campanha!;
        var segunda = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with
        {
            Campanha = new ReferenciaExterna(idCampanha, "Outubro | Serviço Y"),
        });
        var terceira = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with
        {
            Campanha = new ReferenciaExterna(idCampanha, "Outro nome"),
        });

        Assert.Equal($"Google Ads · campanha {idCampanha}", provisoria.Nome);
        Assert.True(provisoria.NomeProvisorio);
        var definitiva = (await fabrica.BuscarLeadAsync(terceira.LeadId)).Campanha!;
        Assert.Equal(provisoria.Id, definitiva.Id);
        Assert.Equal(provisoria.Id, (await fabrica.BuscarLeadAsync(segunda.LeadId)).CampanhaId);
        Assert.Equal("Outubro | Serviço Y", definitiva.Nome);
        Assert.False(definitiva.NomeProvisorio);
    }

    [Fact]
    public async Task A_mesma_campanha_em_origens_diferentes_sao_campanhas_diferentes()
    {
        var idCampanha = LeadsDeTeste.IdExternoUnico();

        var meta = await fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Campanha = new ReferenciaExterna(idCampanha, "Meta") });
        var google = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.GoogleAds) with { Campanha = new ReferenciaExterna(idCampanha) });

        Assert.NotEqual((await fabrica.BuscarLeadAsync(meta.LeadId)).CampanhaId, (await fabrica.BuscarLeadAsync(google.LeadId)).CampanhaId);
    }

    [Fact]
    public async Task Valores_longos_demais_sao_cortados_sem_recusar_a_entrada()
    {
        var resultado = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(nome: new string('a', 200)) with
        {
            Empresa = new string('b', 200),
            Telefone = new string('9', 80),
            Utm = new Utm(Campaign: new string('c', 300)),
        });

        var lead = await fabrica.BuscarLeadAsync(resultado.LeadId);
        Assert.Equal(Lead.TamanhoMaximoNome, lead.Nome.Length);
        Assert.Equal(Lead.TamanhoMaximoEmpresa, lead.Empresa!.Length);
        Assert.Equal(Lead.TamanhoMaximoTelefone, lead.Telefone!.Length);
        Assert.Equal(Lead.TamanhoMaximoRastreio, lead.UtmCampaign!.Length);
    }

    [Fact]
    public async Task Criacao_fica_na_auditoria_so_com_a_origem()
    {
        var resultado = await fabrica.ReceberAsync(LeadsDeTeste.Entrada(Origem.Site));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var registro = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Auditoria
            .SingleAsync(r => r.Entidade == EntidadesAuditoria.Lead && r.EntidadeId == resultado.LeadId.ToString(), Ct);
        Assert.Equal(AcoesAuditoria.LeadCriado, registro.Acao);
        Assert.Null(registro.UsuarioId);
        var detalhes = registro.Detalhes!.RootElement;
        Assert.Equal("site", detalhes.GetProperty("origem").GetString());
        Assert.Single(detalhes.EnumerateObject());
    }

    [Fact]
    public async Task Entradas_simultaneas_da_mesma_pessoa_criam_um_lead_so()
    {
        var email = LeadsDeTeste.EmailUnico();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => fabrica.ReceberAsync(LeadsDeTeste.Entrada() with { Email = email }), Ct)));

        Assert.Single(resultados.Select(r => r.LeadId).Distinct());
        Assert.Single(resultados, r => r.Resultado == ResultadoEntrada.Criado);
        Assert.Equal(9, resultados.Count(r => r.Resultado == ResultadoEntrada.Vinculado));
        Assert.Equal(10, (await EntradasDoLeadAsync(resultados[0].LeadId)).Count);
    }

    [Fact]
    public async Task Reenvios_simultaneos_da_mesma_entrada_criam_um_lead_so()
    {
        var entrada = LeadsDeTeste.Entrada();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => fabrica.ReceberAsync(entrada), Ct)));

        Assert.Single(resultados.Select(r => r.LeadId).Distinct());
        Assert.Single(resultados, r => r.Resultado == ResultadoEntrada.Criado);
        Assert.Equal(4, resultados.Count(r => r.Resultado == ResultadoEntrada.JaRecebido));
        Assert.Single(await EntradasDoLeadAsync(resultados[0].LeadId));
    }
}
