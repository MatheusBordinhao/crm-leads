using System.Net;
using System.Net.Http.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.TempoReal;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// O interceptador do tempo real entrega o que mudou só depois do commit, uma vez por transação (Passo 7). Estes testes
/// usam uma API sem o publicador e leem a fila direto, para conferir o que foi entregue, quantas vezes e quando.
/// </summary>
public class InterceptadorTempoRealTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A API sem o publicador: o que o interceptador entrega fica na fila, para o teste ler.</summary>
    private WebApplicationFactory<Program> ApiSemPublicador() =>
        fabrica.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Remove(services.Single(s => s.ImplementationType == typeof(PublicadorTempoReal)))));

    /// <summary>O que o interceptador entregou desde a última leitura.</summary>
    private static List<MudancasTempoReal> Entregues(WebApplicationFactory<Program> api)
    {
        var leitor = api.Services.GetRequiredService<FilaTempoReal>().Leitor;
        var entregues = new List<MudancasTempoReal>();
        while (leitor.TryRead(out var mudancas))
        {
            entregues.Add(mudancas);
        }

        return entregues;
    }

    private static Interacao Nota(long leadId, Guid usuarioId) => new()
    {
        LeadId = leadId,
        UsuarioId = usuarioId,
        Tipo = TipoInteracao.Nota,
        Descricao = "Nota do teste",
        OcorridoEm = DateTimeOffset.UtcNow,
        CriadoEm = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Os_SaveChanges_de_uma_transacao_viram_um_aviso_so_depois_do_commit()
    {
        await using var api = ApiSemPublicador();

        // A entrada grava o lead, a entrada e os avisos em três SaveChanges, numa transação só.
        var leadId = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;

        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([leadId], entregue.Leads);
        Assert.NotEmpty(entregue.Notificacoes);
    }

    [Fact]
    public async Task Dentro_de_uma_transacao_nada_sai_antes_do_commit()
    {
        await using var api = ApiSemPublicador();
        var usuario = await api.CriarUsuarioAsync();
        var leadId = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;
        Entregues(api);
        await using var escopo = api.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        await using var transacao = await db.Database.BeginTransactionAsync(Ct);
        db.Interacoes.Add(Nota(leadId, usuario.Id));
        await db.SaveChangesAsync(Ct);
        Assert.Empty(Entregues(api));
        await transacao.CommitAsync(Ct);

        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([leadId], entregue.Leads);
    }

    [Fact]
    public async Task Rollback_nao_avisa_nem_depois()
    {
        await using var api = ApiSemPublicador();
        var usuario = await api.CriarUsuarioAsync();
        var desfeito = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;
        var outro = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;
        Entregues(api);
        await using var escopo = api.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        await using (var transacao = await db.Database.BeginTransactionAsync(Ct))
        {
            db.Interacoes.Add(Nota(desfeito, usuario.Id));
            await db.SaveChangesAsync(Ct);
            await transacao.RollbackAsync(Ct);
        }

        Assert.Empty(Entregues(api));
        db.Interacoes.Add(Nota(outro, usuario.Id));
        await db.SaveChangesAsync(Ct);
        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([outro], entregue.Leads);
    }

    [Fact]
    public async Task Transacao_descartada_sem_commit_nao_avisa_nem_depois()
    {
        await using var api = ApiSemPublicador();
        var usuario = await api.CriarUsuarioAsync();
        var descartado = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;
        var outro = (await api.ReceberAsync(LeadsDeTeste.Entrada())).LeadId;
        Entregues(api);
        await using var escopo = api.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();

        // Como numa action que responde 404 ou 400 no meio da transação.
        await using (await db.Database.BeginTransactionAsync(Ct))
        {
            db.Interacoes.Add(Nota(descartado, usuario.Id));
            await db.SaveChangesAsync(Ct);
        }

        Assert.Empty(Entregues(api));
        db.Interacoes.Add(Nota(outro, usuario.Id));
        await db.SaveChangesAsync(Ct);
        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([outro], entregue.Leads);
    }

    [Fact]
    public async Task A_edicao_que_nao_abre_transacao_avisa_e_o_conflito_de_versao_nao()
    {
        await using var api = ApiSemPublicador();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await admin.CriarLeadIdAsync(corpo);
        var versao = (await (await admin.GetAsync($"/api/leads/{leadId}", Ct)).LerJsonAsync()).GetProperty("versao").GetUInt32();
        Entregues(api);
        Dictionary<string, object?> Edicao(string empresa, uint comVersao)
        {
            var edicao = new Dictionary<string, object?>(corpo) { ["empresa"] = empresa, ["versao"] = comVersao };
            edicao.Remove("origem");
            edicao.Remove("responsavelId");
            return edicao;
        }

        var conflito = await admin.PutAsJsonAsync($"/api/leads/{leadId}", Edicao("Empresa do conflito", versao + 1000), Ct);
        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        Assert.Empty(Entregues(api));

        var edicao = await admin.PutAsJsonAsync($"/api/leads/{leadId}", Edicao("Empresa nova", versao), Ct);
        Assert.Equal(HttpStatusCode.OK, edicao.StatusCode);
        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([leadId], entregue.Leads);
    }

    [Fact]
    public async Task A_troca_de_responsavel_anota_quem_tinha_o_lead()
    {
        await using var api = ApiSemPublicador();
        var antes = await api.CriarUsuarioAsync();
        var depois = await api.CriarUsuarioAsync();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = antes.Id;
        var leadId = await admin.CriarLeadIdAsync(corpo);
        Entregues(api);

        var troca = await admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { responsavelId = depois.Id }, Ct);

        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        var entregue = Assert.Single(Entregues(api));
        Assert.Equal([leadId], entregue.Leads);
        Assert.Equal([new TrocaDeResponsavel(leadId, antes.Id)], entregue.Trocas);
        Assert.Contains(entregue.Notificacoes, n => n.UsuarioId == depois.Id);
    }

    [Fact]
    public async Task Excluir_um_lead_de_teste_avisa_o_id_dele()
    {
        await using var api = ApiSemPublicador();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        var leadId = (await api.ReceberAsync(LeadsDeTeste.Entrada() with { Teste = true })).LeadId;
        Entregues(api);

        var exclusao = await admin.DeleteAsync($"/api/leads/{leadId}", Ct);

        // O publicador não acha mais o lead e manda o id só para os admins, que são os únicos que viam o lead de teste.
        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);
        Assert.Equal([leadId], Assert.Single(Entregues(api)).Leads);
    }

    [Fact]
    public async Task Uma_etapa_nova_avisa_que_o_funil_mudou()
    {
        await using var api = ApiSemPublicador();
        using var admin = await api.LogarComoAsync(Perfil.Admin);
        Entregues(api);

        await admin.CriarEtapaAsync();

        var entregue = Assert.Single(Entregues(api));
        Assert.True(entregue.Funil);
        Assert.Empty(entregue.Leads);
    }
}
