using System.Net;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Crm.IntegrationTests;

public class FunilTests(CrmApiFactory fabrica)
{
    private static readonly string[] EtapasIniciais = ["Novo", "Em contato", "Qualificado", "Proposta enviada", "Ganho", "Perdido"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Atendente_ve_as_etapas_iniciais_na_ordem_com_o_tipo()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);

        var resposta = await atendente.GetAsync("/api/etapas", Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var etapas = (await resposta.LerJsonAsync()).EnumerateArray()
            .Where(e => EtapasIniciais.Contains(e.GetProperty("nome").GetString()))
            .Select(e => (Nome: e.GetProperty("nome").GetString(), Tipo: e.GetProperty("tipo").GetString()))
            .ToList();
        Assert.Equal(EtapasIniciais, etapas.Select(e => e.Nome));
        Assert.Equal(["aberta", "aberta", "aberta", "aberta", "ganho", "perdido"], etapas.Select(e => e.Tipo));
    }

    [Fact]
    public async Task Motivos_de_perda_e_configuracoes_vem_com_os_valores_iniciais()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        var motivos = await db.MotivosPerda.Select(m => m.Nome).ToListAsync(Ct);
        var configuracoes = await db.Configuracoes.ToDictionaryAsync(c => c.Chave, c => c.Valor, Ct);

        Assert.Subset(
            new HashSet<string> { "Sem resposta", "Sem interesse", "Preço", "Fechou com concorrente", "Fora do perfil", "Contato inválido" },
            motivos.ToHashSet());
        Assert.Equal("true", configuracoes[ChavesConfiguracao.DistribuicaoAutomatica]);
        Assert.Equal("true", configuracoes[ChavesConfiguracao.ExigirDocumentoParaGanho]);
    }

    private static async Task<JsonElement> QuadroAsync(HttpClient cliente, string query = "")
    {
        var resposta = await cliente.GetAsync($"/api/funil{query}", Ct);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.LerJsonAsync();
    }

    private static JsonElement Coluna(JsonElement quadro, string etapa) =>
        quadro.GetProperty("colunas").EnumerateArray().Single(c => c.GetProperty("etapa").GetProperty("nome").GetString() == etapa);

    private static List<long> Ids(JsonElement coluna) =>
        [.. coluna.GetProperty("leads").EnumerateArray().Select(l => l.GetProperty("id").GetInt64())];

    private static List<long> IdsNoQuadro(JsonElement quadro) =>
        [.. quadro.GetProperty("colunas").EnumerateArray().SelectMany(Ids)];

    /// <summary>Grava a data de perda ou de conversão direto no banco, como se a mudança tivesse sido em outro dia.</summary>
    private async Task MudarDataDoFechamentoAsync(long leadId, DateTimeOffset quando)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .Where(l => l.Id == leadId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.PerdidoEm, l => l.PerdidoEm == null ? null : quando)
                    .SetProperty(l => l.ConvertidoEm, l => l.ConvertidoEm == null ? null : quando),
                Ct);
    }

    [Fact]
    public async Task Quadro_mostra_as_colunas_abertas_na_ordem_com_os_leads_da_atendente()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(usuario);
        var corpoNovo = LeadsDeTeste.CorpoLead("Lead no Novo");
        corpoNovo["empresa"] = "Empresa X";
        var noNovo = await atendente.CriarLeadIdAsync(corpoNovo);
        var emContato = await atendente.CriarLeadIdAsync();
        await atendente.MudarEtapaAsync(emContato, (await fabrica.EtapaAsync("Em contato")).Id);
        var perdido = await atendente.CriarLeadIdAsync();
        await atendente.MudarEtapaAsync(perdido, (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync());
        var corpoGanho = LeadsDeTeste.CorpoLead();
        corpoGanho["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        var ganho = await atendente.CriarLeadIdAsync(corpoGanho);
        await atendente.MudarEtapaAsync(ganho, (await fabrica.EtapaAsync("Ganho")).Id);

        var quadro = await QuadroAsync(atendente);

        var colunas = quadro.GetProperty("colunas").EnumerateArray().ToList();
        Assert.All(colunas, c => Assert.Equal("aberta", c.GetProperty("etapa").GetProperty("tipo").GetString()));
        Assert.Equal(
            ["Novo", "Em contato", "Qualificado", "Proposta enviada"],
            colunas.Select(c => c.GetProperty("etapa").GetProperty("nome").GetString()).Where(n => EtapasIniciais.Contains(n)));
        Assert.Equal([noNovo], Ids(Coluna(quadro, "Novo")));
        Assert.Equal(1, Coluna(quadro, "Novo").GetProperty("total").GetInt32());
        Assert.Equal([emContato], Ids(Coluna(quadro, "Em contato")));
        Assert.DoesNotContain(perdido, IdsNoQuadro(quadro));
        Assert.DoesNotContain(ganho, IdsNoQuadro(quadro));
        Assert.Equal(1, quadro.GetProperty("ganho").GetProperty("periodo").GetInt32());
        Assert.Equal(1, quadro.GetProperty("perdido").GetProperty("periodo").GetInt32());
        Assert.Equal("Ganho", quadro.GetProperty("ganho").GetProperty("etapa").GetProperty("nome").GetString());

        // O cartão não leva e-mail, telefone nem documento.
        var cartao = Coluna(quadro, "Novo").GetProperty("leads")[0];
        Assert.Equal(["id", "nome", "empresa", "origem", "responsavel", "teste", "criadoEm"], cartao.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Lead no Novo", "Empresa X", "manual"), (cartao.GetProperty("nome").GetString(), cartao.GetProperty("empresa").GetString(), cartao.GetProperty("origem").GetString()));
        Assert.Equal(usuario.Nome, cartao.GetProperty("responsavel").GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Ganho_e_Perdido_contam_so_os_ultimos_30_dias_no_horario_de_Brasilia()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var perdido = (await fabrica.EtapaAsync("Perdido")).Id;
        var motivo = await fabrica.MotivoPerdaIdAsync();
        var leads = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var lead = await atendente.CriarLeadIdAsync();
            await atendente.MudarEtapaAsync(lead, perdido, motivo);
            leads.Add(lead);
        }

        var hojeAntes = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var quadro = await QuadroAsync(atendente);
        var hojeDepois = HorarioBrasilia.Dia(DateTimeOffset.UtcNow);
        var desde = DateOnly.Parse(quadro.GetProperty("desde").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        var inicio = HorarioBrasilia.InicioDoDia(desde);
        await MudarDataDoFechamentoAsync(leads[1], inicio);
        await MudarDataDoFechamentoAsync(leads[2], inicio.AddSeconds(-1));

        var depois = await QuadroAsync(atendente);

        Assert.InRange(desde, hojeAntes.AddDays(-29), hojeDepois.AddDays(-29));
        Assert.Equal(3, quadro.GetProperty("perdido").GetProperty("periodo").GetInt32());
        Assert.Equal(2, depois.GetProperty("perdido").GetProperty("periodo").GetInt32());
        Assert.Equal(0, depois.GetProperty("ganho").GetProperty("periodo").GetInt32());
    }

    [Fact]
    public async Task Filtros_do_quadro_sao_os_mesmos_da_lista()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var marca = Guid.NewGuid().ToString("N")[..12];
        var corpoMeta = LeadsDeTeste.CorpoIntegracao();
        corpoMeta["nome"] = $"Quadro {marca} Meta";
        var meta = await n8n.EnviarLeadIdAsync(corpoMeta);
        var corpoSite = LeadsDeTeste.CorpoIntegracao("site");
        corpoSite["nome"] = $"Quadro {marca} Site";
        var site = await n8n.EnviarLeadIdAsync(corpoSite);
        var campanhaDoSite = (await fabrica.BuscarLeadAsync(site)).CampanhaId;

        var porBusca = IdsNoQuadro(await QuadroAsync(admin, $"?busca={marca}"));
        var porOrigem = IdsNoQuadro(await QuadroAsync(admin, $"?busca={marca}&origem=site"));
        var porCampanha = IdsNoQuadro(await QuadroAsync(admin, $"?campanhaId={campanhaDoSite}"));
        var semResponsavel = IdsNoQuadro(await QuadroAsync(admin, $"?busca={marca}&semResponsavel=true"));

        Assert.Equal([meta, site], porBusca.Order());
        Assert.Equal([site], porOrigem);
        Assert.Equal([site], porCampanha);
        Assert.Equal([meta, site], semResponsavel.Order());
    }

    [Fact]
    public async Task Coluna_mostra_ate_100_cartoes_mais_recentes_e_o_total()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        var novo = await fabrica.EtapaAsync("Novo");
        var agora = DateTimeOffset.UtcNow;
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            db.Leads.AddRange(Enumerable.Range(0, FunilController.CartoesPorColuna + 1).Select(i => new Lead
            {
                Nome = $"Lead {i}",
                EtapaId = novo.Id,
                ResponsavelId = atendente.Id,
                Origem = Origem.Manual,
                CriadoEm = agora.AddMinutes(-i),
                AtualizadoEm = agora,
            }));
            await db.SaveChangesAsync(Ct);
        }

        using var cliente = await fabrica.LogarAsync(atendente);
        var coluna = Coluna(await QuadroAsync(cliente), "Novo");

        Assert.Equal(FunilController.CartoesPorColuna + 1, coluna.GetProperty("total").GetInt32());
        var nomes = coluna.GetProperty("leads").EnumerateArray().Select(l => l.GetProperty("nome").GetString()).ToList();
        Assert.Equal(FunilController.CartoesPorColuna, nomes.Count);
        Assert.Equal("Lead 0", nomes[0]);
        Assert.DoesNotContain($"Lead {FunilController.CartoesPorColuna}", nomes);
    }

    [Fact]
    public async Task Nao_pode_haver_duas_etapas_ativas_de_ganho()
    {
        await using var conexao = new NpgsqlConnection(fabrica.ConnectionString);
        await conexao.OpenAsync(Ct);
        await using var transacao = await conexao.BeginTransactionAsync(Ct);
        await using var comando = new NpgsqlCommand(
            "INSERT INTO etapas (nome, ordem, tipo, ativa) VALUES ('Outro ganho', 99, 'ganho', true)", conexao, transacao);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(Ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, erro.SqlState);
    }
}
