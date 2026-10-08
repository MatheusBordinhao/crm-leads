using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;

namespace Crm.IntegrationTests;

/// <summary>
/// Relatório por campanha no banco compartilhado, pelos caminhos de verdade (integração e tela), e o critério de
/// aceite de que ele não tem nenhum dado pessoal. Cada teste usa uma campanha única, porque o relatório conta todos os
/// leads do banco. Os números de cada definição são conferidos com a massa conhecida, em
/// <see cref="RelatorioMassaConhecidaTests"/>.
/// </summary>
public class RelatorioTests(CrmApiFactory fabrica)
{
    private const string Relatorio = "/api/relatorios/campanhas";

    private const string RelatorioCsv = "/api/relatorios/campanhas/csv";

    private const string RelatorioDaIntegracao = "/api/integracoes/relatorios/campanhas";

    private const string RelatorioCsvDaIntegracao = "/api/integracoes/relatorios/campanhas/csv";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> JsonAsync(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    /// <summary>O corpo da resposta como texto, com o BOM do CSV, se houver.</summary>
    private static async Task<string> TextoAsync(HttpClient cliente, string caminho)
    {
        var resposta = await cliente.GetAsync(caminho, Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return Encoding.UTF8.GetString(await resposta.Content.ReadAsByteArrayAsync(Ct));
    }

    private static double? Numero(JsonElement valor) => valor.ValueKind == JsonValueKind.Null ? null : valor.GetDouble();

    private static JsonElement LinhaDaCampanha(JsonElement relatorio, int campanhaId) =>
        relatorio.GetProperty("linhas").EnumerateArray()
            .Single(l => l.GetProperty("campanha") is { ValueKind: JsonValueKind.Object } c && c.GetProperty("id").GetInt32() == campanhaId);

    private static (int, int, int, int, int, double?, double?) NumerosDaLinha(JsonElement linha) => (
        linha.GetProperty("leadsNovos").GetInt32(),
        linha.GetProperty("entradas").GetInt32(),
        linha.GetProperty("emAndamento").GetInt32(),
        linha.GetProperty("ganhos").GetInt32(),
        linha.GetProperty("perdidos").GetInt32(),
        Numero(linha.GetProperty("taxaConversao")),
        Numero(linha.GetProperty("medianaPrimeiroContatoMinutos")));

    /// <summary>Os mesmos números no painel: em andamento são as etapas abertas da situação atual, e perdidos, a Perdido.</summary>
    private static (int, int, int, int, int, double?, double?) NumerosDoPainel(JsonElement painel)
    {
        var situacao = painel.GetProperty("situacaoAtual").EnumerateArray()
            .Select(s => (Tipo: s.GetProperty("etapa").GetProperty("tipo").GetString(), Leads: s.GetProperty("leadsNovos").GetInt32()))
            .ToList();
        return (
            painel.GetProperty("leadsNovos").GetInt32(),
            painel.GetProperty("entradas").GetInt32(),
            situacao.Where(s => s.Tipo == "aberta").Sum(s => s.Leads),
            painel.GetProperty("ganhos").GetInt32(),
            situacao.Where(s => s.Tipo == "perdido").Sum(s => s.Leads),
            Numero(painel.GetProperty("taxaConversao")),
            Numero(painel.GetProperty("primeiroContato").GetProperty("medianaMinutos")));
    }

    private static List<string> Chaves(JsonElement objeto) => [.. objeto.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Linha_da_campanha_bate_com_o_painel_pelos_caminhos_de_verdade()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var campanha = new { id = LeadsDeTeste.IdExternoUnico(), nome = $"Relatório {LeadsDeTeste.Marca()}" };
        Dictionary<string, object?> Corpo()
        {
            var corpo = LeadsDeTeste.CorpoIntegracao();
            corpo["campanha"] = campanha;
            corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
            return corpo;
        }

        var corpoDoGanho = Corpo();
        var ganho = await n8n.EnviarLeadIdAsync(corpoDoGanho);
        var perdido = await n8n.EnviarLeadIdAsync(Corpo());
        await n8n.EnviarLeadIdAsync(Corpo());

        // Uma ligação e o Ganho no primeiro; Perdido no segundo, o que também conta como contato; o terceiro fica em Novo.
        (await admin.RegistrarInteracaoAsync(ganho, "ligacao")).EnsureSuccessStatusCode();
        (await admin.MudarEtapaAsync(ganho, (await fabrica.EtapaAsync("Ganho")).Id)).EnsureSuccessStatusCode();
        (await admin.MudarEtapaAsync(perdido, (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync("Preço"))).EnsureSuccessStatusCode();

        // A pessoa que virou cliente volta pela mesma campanha: mais uma entrada, sem lead novo.
        var volta = Corpo();
        volta["email"] = corpoDoGanho["email"];
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(volta)).LerJsonAsync()).GetProperty("situacao").GetString());
        var campanhaId = (await fabrica.BuscarLeadAsync(ganho)).CampanhaId!.Value;

        var linha = LinhaDaCampanha(await JsonAsync(admin, Relatorio), campanhaId);
        var painel = await JsonAsync(admin, $"/api/painel?campanhaId={campanhaId}");
        var csv = await TextoAsync(admin, RelatorioCsv);

        var numeros = NumerosDaLinha(linha);
        Assert.Equal((3, 4, 1, 1, 1, (double?)(1.0 / 3)), (numeros.Item1, numeros.Item2, numeros.Item3, numeros.Item4, numeros.Item5, numeros.Item6));
        Assert.Equal("meta_lead_ads", linha.GetProperty("origem").GetString());
        Assert.Equal(NumerosDoPainel(painel), numeros);

        // No CSV, a linha da campanha traz os mesmos números.
        var linhaDoCsv = csv.Split("\r\n").Single(l => l.StartsWith($"Meta;{campanha.nome};", StringComparison.Ordinal));
        Assert.Equal(
            $"Meta;{campanha.nome};3;4;1;1;1;33,3%;{RelatorioCampanhasCsv.Minutos(numeros.Item7)}",
            linhaDoCsv);
    }

    [Fact]
    public async Task O_relatorio_nao_tem_nenhum_dado_pessoal()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var n8n = fabrica.CriarClienteIntegracao();
        var marca = LeadsDeTeste.Marca();

        var daIntegracao = LeadsDeTeste.CorpoIntegracao();
        daIntegracao["nome"] = $"Pessoa {marca} da Silva";
        daIntegracao["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        daIntegracao["empresa"] = $"Empresa {marca} Ltda";
        daIntegracao["campanha"] = new { id = LeadsDeTeste.IdExternoUnico(), nome = $"Campanha {marca}" };
        daIntegracao["respostasExtras"] = new Dictionary<string, string> { ["Qual serviço você procura?"] = $"Resposta {marca}" };
        daIntegracao["gclid"] = $"gclid-{marca}";
        await n8n.EnviarLeadIdAsync(daIntegracao);

        var daTela = LeadsDeTeste.CorpoLead($"Cadastro {marca} Souza");
        daTela["telefone"] = LeadsDeTeste.TelefoneUnico();
        daTela["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        daTela["empresa"] = $"Comércio {marca}";
        await atendente.CriarLeadIdAsync(daTela);

        string[] dadosPessoais =
        [
            .. new[] { daIntegracao, daTela }.SelectMany(corpo => new[] { "nome", "email", "telefone", "cpfCnpj", "empresa" }
                .Select(campo => (string)corpo[campo]!)),
            ((string)daIntegracao["telefone"]!)[1..],
            ((string)daTela["telefone"]!)[1..],
            $"Resposta {marca}",
            $"gclid-{marca}",
        ];

        var respostas = new[]
        {
            await TextoAsync(admin, Relatorio),
            await TextoAsync(admin, RelatorioCsv),
            await TextoAsync(n8n, RelatorioDaIntegracao),
            await TextoAsync(n8n, RelatorioCsvDaIntegracao),
        };

        // A campanha do lead está nas quatro respostas, sem nenhum dado das pessoas.
        Assert.All(respostas, texto => Assert.Contains($"Campanha {marca}", texto, StringComparison.Ordinal));
        foreach (var dado in dadosPessoais)
        {
            Assert.All(respostas, texto => Assert.DoesNotContain(dado, texto, StringComparison.OrdinalIgnoreCase));
        }

        // O JSON só tem contagens, taxas e a campanha, numa lista fechada de campos; o CSV, as colunas do cabeçalho.
        List<string> numeros = ["emAndamento", "entradas", "ganhos", "leadsNovos", "medianaPrimeiroContatoMinutos", "perdidos", "taxaConversao"];
        var json = JsonSerializer.Deserialize<JsonElement>(respostas[0]);
        Assert.Equal(["ate", "de", "linhas", "total"], Chaves(json));
        Assert.Equal(numeros, Chaves(json.GetProperty("total")));
        Assert.All(json.GetProperty("linhas").EnumerateArray(), linha =>
        {
            Assert.Equal([.. numeros.Append("campanha").Append("origem").Order(StringComparer.Ordinal)], Chaves(linha));
            if (linha.GetProperty("campanha").ValueKind != JsonValueKind.Null)
            {
                Assert.Equal(["id", "nome"], Chaves(linha.GetProperty("campanha")));
            }
        });
        Assert.All(
            respostas.Where((_, i) => i % 2 == 1),
            csv => Assert.StartsWith($"﻿{string.Join(';', RelatorioCampanhasCsv.Cabecalho)}\r\n", csv, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Campanha_so_com_leads_de_teste_nao_aparece()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var nome = $"Campanha de teste {LeadsDeTeste.Marca()}";
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["campanha"] = new { id = LeadsDeTeste.IdExternoUnico(), nome };
        corpo["teste"] = true;
        await n8n.EnviarLeadIdAsync(corpo);

        var relatorio = await TextoAsync(admin, Relatorio);
        var csv = await TextoAsync(admin, RelatorioCsv);

        Assert.DoesNotContain(nome, relatorio, StringComparison.Ordinal);
        Assert.DoesNotContain(nome, csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sem_datas_a_tela_mostra_os_ultimos_30_dias_com_hoje_no_horario_de_Brasilia()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        var hojeAntes = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var relatorio = await JsonAsync(admin, Relatorio);
        var hojeDepois = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);

        DateOnly Dia(string campo) => DateOnly.Parse(relatorio.GetProperty(campo).GetString()!, CultureInfo.InvariantCulture);
        Assert.InRange(Dia("ate"), hojeAntes, hojeDepois);
        Assert.Equal(Dia("ate").AddDays(-29), Dia("de"));
    }

    [Fact]
    public async Task Atendente_recebe_403_e_quem_nao_tem_sessao_401()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        using var anonimo = fabrica.CriarClienteHttps();

        foreach (var caminho in new[] { Relatorio, RelatorioCsv })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await atendente.GetAsync(caminho, Ct)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync(caminho, Ct)).StatusCode);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("chave-errada-mas-com-o-mesmo-tamanho-0123")]
    public async Task Relatorio_da_integracao_sem_a_chave_certa_responde_401(string? chave)
    {
        using var n8n = fabrica.CriarClienteIntegracao(chave);

        foreach (var caminho in new[] { RelatorioDaIntegracao, RelatorioCsvDaIntegracao })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await n8n.GetAsync(caminho, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task Sessao_do_admin_nao_vale_no_relatorio_da_integracao()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);

        foreach (var caminho in new[] { RelatorioDaIntegracao, RelatorioCsvDaIntegracao })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync(caminho, Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task O_limite_por_minuto_da_integracao_vale_para_o_relatorio()
    {
        // Fábrica à parte, com um limite baixo e um contador só dela.
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("Integracao:RequisicoesPorMinuto", "2"));
        using var n8n = api.CriarClienteIntegracao();

        Assert.Equal(HttpStatusCode.OK, (await n8n.GetAsync(RelatorioDaIntegracao, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await n8n.GetAsync(RelatorioCsvDaIntegracao, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await n8n.GetAsync(RelatorioDaIntegracao, Ct)).StatusCode);
    }

    [Fact]
    public async Task Periodo_invalido_responde_400_com_a_mensagem_no_campo_nas_quatro_rotas()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();

        foreach (var (cliente, caminho) in new[] { (admin, Relatorio), (admin, RelatorioCsv), (n8n, RelatorioDaIntegracao), (n8n, RelatorioCsvDaIntegracao) })
        {
            var invertido = await cliente.GetAsync($"{caminho}?de=2026-09-30&ate=2026-09-01", Ct);
            var antigo = await cliente.GetAsync($"{caminho}?de=1999-12-31", Ct);

            Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);
            Assert.Equal([PeriodoPainel.MensagemPeriodo], await invertido.LerErrosDoCampoAsync("ate"));
            Assert.Equal(HttpStatusCode.BadRequest, antigo.StatusCode);
            Assert.Equal([PeriodoPainel.MensagemData], await antigo.LerErrosDoCampoAsync("de"));
        }
    }
}
