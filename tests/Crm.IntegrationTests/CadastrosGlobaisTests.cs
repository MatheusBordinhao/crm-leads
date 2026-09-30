using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Regras que olham todas as etapas ou todos os motivos: a ordem do funil, a última etapa aberta ativa e o
/// último motivo ativo. Rodam sozinhos e deixam o banco como estava.
/// </summary>
[Collection(nameof(EstadoGlobal))]
public class CadastrosGlobaisTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int IdDe(JsonElement etapa) => etapa.GetProperty("id").GetInt32();

    private static async Task<List<JsonElement>> EtapasAsync(HttpClient cliente) =>
        [.. (await (await cliente.GetAsync("/api/etapas", Ct)).LerJsonAsync()).EnumerateArray()];

    [Fact]
    public async Task Admin_reordena_as_abertas_e_Ganho_e_Perdido_ficam_no_fim()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var primeira = IdDe(await admin.CriarEtapaAsync());
        var segunda = IdDe(await admin.CriarEtapaAsync());
        var abertas = (await EtapasAsync(admin))
            .Where(e => e.GetProperty("tipo").GetString() == "aberta")
            .Select(IdDe)
            .ToList();
        var novaOrdem = abertas.Select(id => id == primeira ? segunda : id == segunda ? primeira : id).ToList();

        var resposta = await admin.PutAsJsonAsync("/api/etapas/ordem", new { ids = novaOrdem }, Ct);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var etapas = (await resposta.LerJsonAsync()).EnumerateArray().ToList();
        Assert.Equal(novaOrdem, etapas.Where(e => e.GetProperty("tipo").GetString() == "aberta").Select(IdDe));
        Assert.Equal(Enumerable.Range(1, etapas.Count), etapas.Select(e => e.GetProperty("ordem").GetInt32()));
        Assert.Equal(
            ["ganho", "perdido"],
            etapas.Skip(novaOrdem.Count).Where(e => e.GetProperty("ativa").GetBoolean()).Select(e => e.GetProperty("tipo").GetString()));
        Assert.Equal((await fabrica.EtapaAsync("Novo")).Id, novaOrdem[0]);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var registro = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Auditoria
            .Where(r => r.Acao == AcoesAuditoria.EtapasReordenadas)
            .OrderByDescending(r => r.Id)
            .FirstAsync(Ct);
        Assert.Equal(novaOrdem, registro.Detalhes!.RootElement.GetProperty("ids").EnumerateArray().Select(i => i.GetInt32()));
    }

    [Fact]
    public async Task Ordem_sem_todas_as_abertas_ou_com_Ganho_e_recusada()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var etapas = await EtapasAsync(admin);
        var abertas = etapas.Where(e => e.GetProperty("tipo").GetString() == "aberta").Select(IdDe).ToList();
        var ganho = IdDe(etapas.Single(e => e.GetProperty("tipo").GetString() == "ganho" && e.GetProperty("ativa").GetBoolean()));

        var faltando = await admin.PutAsJsonAsync("/api/etapas/ordem", new { ids = abertas.Skip(1) }, Ct);
        var comGanho = await admin.PutAsJsonAsync("/api/etapas/ordem", new { ids = abertas.Skip(1).Append(ganho) }, Ct);
        var repetida = await admin.PutAsJsonAsync("/api/etapas/ordem", new { ids = abertas.Append(abertas[0]) }, Ct);

        foreach (var resposta in new[] { faltando, comGanho, repetida })
        {
            Assert.Equal([RegrasEtapas.MensagemOrdemIncompleta], await resposta.LerErrosDoCampoAsync("ids"));
        }

        Assert.Equal(abertas, (await EtapasAsync(admin)).Where(e => e.GetProperty("tipo").GetString() == "aberta").Select(IdDe));
    }

    [Fact]
    public async Task A_ultima_etapa_aberta_ativa_nao_e_desativada()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var novo = await fabrica.EtapaAsync("Novo");
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var outras = await db.Etapas
            .Where(e => e.Ativa && e.Tipo == TipoEtapa.Aberta && e.Id != novo.Id)
            .Select(e => e.Id)
            .ToListAsync(Ct);
        await db.Etapas.Where(e => outras.Contains(e.Id)).ExecuteUpdateAsync(s => s.SetProperty(e => e.Ativa, false), Ct);
        try
        {
            var resposta = await admin.PostAsJsonAsync($"/api/etapas/{novo.Id}/desativar", new { moverLeadsParaId = (int?)null }, Ct);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal(RegrasEtapas.MensagemUltimaAberta, await resposta.LerDetalheAsync());
            Assert.True((await fabrica.EtapaAsync("Novo")).Ativa);
        }
        finally
        {
            await db.Etapas.Where(e => outras.Contains(e.Id)).ExecuteUpdateAsync(s => s.SetProperty(e => e.Ativa, true), Ct);
        }
    }

    [Fact]
    public async Task O_ultimo_motivo_ativo_nao_e_desativado()
    {
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var ultimo = await db.MotivosPerda.SingleAsync(m => m.Nome == "Sem resposta", Ct);
        var outros = await db.MotivosPerda.Where(m => m.Ativo && m.Id != ultimo.Id).Select(m => m.Id).ToListAsync(Ct);
        await db.MotivosPerda.Where(m => outros.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.Ativo, false), Ct);
        try
        {
            var resposta = await admin.PutAsJsonAsync($"/api/motivos-perda/{ultimo.Id}", new { nome = ultimo.Nome, ativo = false }, Ct);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal(MotivosPerdaController.MensagemUltimoAtivo, await resposta.LerDetalheAsync());
        }
        finally
        {
            await db.MotivosPerda.Where(m => outros.Contains(m.Id)).ExecuteUpdateAsync(s => s.SetProperty(m => m.Ativo, true), Ct);
        }

        Assert.True((await db.MotivosPerda.AsNoTracking().SingleAsync(m => m.Id == ultimo.Id, Ct)).Ativo);
    }
}
