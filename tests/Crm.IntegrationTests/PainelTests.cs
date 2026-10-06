using System.Globalization;
using System.Net;
using System.Text.Json;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;

namespace Crm.IntegrationTests;

/// <summary>
/// Painel no banco compartilhado, pelos caminhos de verdade (integração e tela). Os números de cada definição são
/// conferidos com a massa conhecida, em <see cref="PainelMassaConhecidaTests"/>.
/// </summary>
public class PainelTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<JsonElement> PainelAsync(HttpClient cliente, string query = "")
    {
        var resposta = await cliente.GetAsync($"/api/painel{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static int Etapa(JsonElement painel, string nome) =>
        painel.GetProperty("situacaoAtual").EnumerateArray()
            .Single(s => s.GetProperty("etapa").GetProperty("nome").GetString() == nome)
            .GetProperty("leadsNovos").GetInt32();

    [Fact]
    public async Task Leads_de_teste_ficam_fora_de_todos_os_numeros()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var campanha = new { id = LeadsDeTeste.IdExternoUnico(), nome = "Campanha do painel" };
        Dictionary<string, object?> Corpo(bool teste)
        {
            var corpo = LeadsDeTeste.CorpoIntegracao();
            corpo["campanha"] = campanha;
            corpo["teste"] = teste;
            return corpo;
        }

        var real = await n8n.EnviarLeadIdAsync(Corpo(teste: false));
        var corpoDeTeste = Corpo(teste: true);
        await n8n.EnviarLeadIdAsync(corpoDeTeste);
        // A mesma pessoa de teste entra de novo: a entrada vai para o lead de teste.
        var repetida = Corpo(teste: true);
        repetida["email"] = corpoDeTeste["email"];
        repetida["telefone"] = corpoDeTeste["telefone"];
        Assert.Equal("vinculado", (await (await n8n.EnviarLeadAsync(repetida)).LerJsonAsync()).GetProperty("situacao").GetString());
        var perdidoDeTeste = await n8n.EnviarLeadIdAsync(Corpo(teste: true));
        var mudanca = await admin.MudarEtapaAsync(perdidoDeTeste, (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync());
        Assert.Equal(HttpStatusCode.OK, mudanca.StatusCode);
        var campanhaId = (await fabrica.BuscarLeadAsync(real)).CampanhaId;

        var painel = await PainelAsync(admin, $"?campanhaId={campanhaId}");

        // Só o lead real conta: os 2 leads de teste, as 3 entradas de teste e a perda de teste ficam de fora.
        Assert.Equal(1, painel.GetProperty("leadsNovos").GetInt32());
        Assert.Equal(1, painel.GetProperty("entradas").GetInt32());
        Assert.Equal(1, Etapa(painel, "Novo"));
        Assert.Equal(0, Etapa(painel, "Perdido"));
        Assert.Equal(1, painel.GetProperty("primeiroContato").GetProperty("semContato").GetInt32());
        Assert.Equal(1, Assert.Single(painel.GetProperty("porOrigem").EnumerateArray()).GetProperty("leadsNovos").GetInt32());
        Assert.Equal(1, Assert.Single(painel.GetProperty("porCampanha").EnumerateArray()).GetProperty("leadsNovos").GetInt32());
        Assert.Equal(1, Assert.Single(painel.GetProperty("porAtendente").EnumerateArray()).GetProperty("leadsNovos").GetInt32());
        Assert.Equal(0, painel.GetProperty("perdas").GetProperty("leads").GetInt32());
        Assert.Empty(painel.GetProperty("perdas").GetProperty("porMotivo").EnumerateArray());
    }

    [Fact]
    public async Task Painel_conta_o_que_a_tela_grava_e_nao_leva_dados_pessoais()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpos = Enumerable.Range(0, 3).Select(_ => LeadsDeTeste.CorpoLead($"Painel {Guid.NewGuid():N}")).ToList();
        corpos[0]["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        var ids = new List<long>();
        foreach (var corpo in corpos)
        {
            ids.Add(await atendente.CriarLeadIdAsync(corpo));
        }

        // O primeiro vira cliente depois de uma ligação; o segundo é perdido, o que também conta como contato.
        (await atendente.RegistrarInteracaoAsync(ids[0], "ligacao")).EnsureSuccessStatusCode();
        (await atendente.MudarEtapaAsync(ids[0], (await fabrica.EtapaAsync("Ganho")).Id)).EnsureSuccessStatusCode();
        (await atendente.MudarEtapaAsync(ids[1], (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync("Preço"))).EnsureSuccessStatusCode();

        var resposta = await atendente.GetAsync("/api/painel", Ct);
        var json = await resposta.Content.ReadAsStringAsync(Ct);
        var painel = JsonSerializer.Deserialize<JsonElement>(json);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(3, painel.GetProperty("leadsNovos").GetInt32());
        Assert.Equal(3, painel.GetProperty("entradas").GetInt32());
        Assert.Equal(1, painel.GetProperty("ganhos").GetInt32());
        Assert.Equal(1.0 / 3, painel.GetProperty("taxaConversao").GetDouble());
        Assert.Equal((1, 1, 1), (Etapa(painel, "Novo"), Etapa(painel, "Ganho"), Etapa(painel, "Perdido")));
        var contato = painel.GetProperty("primeiroContato");
        Assert.Equal((2, 1), (contato.GetProperty("comContato").GetInt32(), contato.GetProperty("semContato").GetInt32()));
        Assert.InRange(contato.GetProperty("medianaMinutos").GetDouble(), 0, 5);
        var origem = Assert.Single(painel.GetProperty("porOrigem").EnumerateArray());
        Assert.Equal(("manual", 3, 1), (origem.GetProperty("origem").GetString(), origem.GetProperty("leadsNovos").GetInt32(), origem.GetProperty("ganhos").GetInt32()));
        Assert.Equal(JsonValueKind.Null, Assert.Single(painel.GetProperty("porCampanha").EnumerateArray()).GetProperty("campanha").ValueKind);
        var motivo = Assert.Single(painel.GetProperty("perdas").GetProperty("porMotivo").EnumerateArray());
        Assert.Equal(("Preço", 1), (motivo.GetProperty("motivo").GetProperty("nome").GetString(), motivo.GetProperty("leads").GetInt32()));
        Assert.Equal(JsonValueKind.Null, painel.GetProperty("porAtendente").ValueKind);
        foreach (var corpo in corpos)
        {
            Assert.DoesNotContain((string)corpo["nome"]!, json, StringComparison.Ordinal);
            Assert.DoesNotContain((string)corpo["email"]!, json, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Periodo_padrao_sao_os_ultimos_30_dias_com_hoje_no_horario_de_Brasilia()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var hojeAntes = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var padrao = await PainelAsync(atendente);
        var hojeDepois = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var soComOFim = await PainelAsync(atendente, "?ate=2026-09-30");

        DateOnly Dia(JsonElement painel, string campo) => DateOnly.Parse(painel.GetProperty(campo).GetString()!, CultureInfo.InvariantCulture);
        Assert.InRange(Dia(padrao, "ate"), hojeAntes, hojeDepois);
        Assert.Equal(Dia(padrao, "ate").AddDays(-29), Dia(padrao, "de"));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), (Dia(soComOFim, "de"), Dia(soComOFim, "ate")));
    }

    [Fact]
    public async Task Periodo_invalido_responde_400_com_a_mensagem_no_campo()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var invertido = await atendente.GetAsync("/api/painel?de=2026-09-30&ate=2026-09-01", Ct);
        var antigo = await atendente.GetAsync("/api/painel?de=1999-12-31", Ct);
        var extremo = await atendente.GetAsync("/api/painel?ate=9999-12-31", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, invertido.StatusCode);
        Assert.Equal([FiltroPainelValidator.MensagemPeriodo], await invertido.LerErrosDoCampoAsync("ate"));
        Assert.Equal(HttpStatusCode.BadRequest, antigo.StatusCode);
        Assert.Equal([FiltroPainelValidator.MensagemData], await antigo.LerErrosDoCampoAsync("de"));
        Assert.Equal(HttpStatusCode.BadRequest, extremo.StatusCode);
        Assert.Equal([FiltroPainelValidator.MensagemData], await extremo.LerErrosDoCampoAsync("ate"));
    }
}
