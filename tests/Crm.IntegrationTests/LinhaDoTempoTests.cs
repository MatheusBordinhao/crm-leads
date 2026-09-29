using System.Globalization;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Linha do tempo da ficha (seção 3.5): <c>GET /api/leads/{id}/linha-do-tempo</c>.</summary>
public class LinhaDoTempoTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Tipo(JsonElement item) => item.GetProperty("tipo").GetString()!;

    private static string? NomeDoUsuario(JsonElement item) =>
        item.GetProperty("usuario").ValueKind == JsonValueKind.Null ? null : item.GetProperty("usuario").GetProperty("nome").GetString();

    private static (string? De, string? Para) Etapas(JsonElement item) =>
        (item.GetProperty("etapa").GetProperty("anterior").GetProperty("nome").GetString(),
         item.GetProperty("etapa").GetProperty("nova").GetProperty("nome").GetString());

    [Fact]
    public async Task Junta_entradas_interacoes_e_mudancas_da_mais_recente_para_a_mais_antiga()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var leadId = await n8n.EnviarLeadIdAsync(corpo);
        await admin.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Em contato")).Id);
        await admin.RegistrarInteracaoAsync(leadId, "nota", "Pediu proposta");
        var outraEntrada = LeadsDeTeste.CorpoIntegracao("google_ads");
        outraEntrada["email"] = corpo["email"];
        outraEntrada["campanha"] = null;
        await n8n.EnviarLeadIdAsync(outraEntrada);

        var itens = await admin.LinhaDoTempoAsync(leadId);

        Assert.Equal(["entrada", "interacao", "etapa", "entrada"], itens.Select(Tipo));
        var datas = itens.Select(i => i.GetProperty("ocorridoEm").GetDateTimeOffset()).ToList();
        Assert.Equal(datas.OrderDescending(), datas);

        var vinculada = itens[0].GetProperty("entrada");
        Assert.Equal(("google_ads", "vinculado"), (vinculada.GetProperty("origem").GetString(), vinculada.GetProperty("situacao").GetString()));
        Assert.Equal(JsonValueKind.Null, vinculada.GetProperty("campanha").ValueKind);
        Assert.Null(NomeDoUsuario(itens[0]));

        Assert.Equal("Pediu proposta", itens[1].GetProperty("interacao").GetProperty("descricao").GetString());
        Assert.Equal(eu.Nome, NomeDoUsuario(itens[1]));
        Assert.Equal(("Novo", "Em contato"), Etapas(itens[2]));
        Assert.Equal(eu.Nome, NomeDoUsuario(itens[2]));

        // A entrada não leva dados pessoais nem o payload: só origem, campanha, situação e se é de teste.
        var primeira = itens[3].GetProperty("entrada");
        Assert.Equal(["origem", "campanha", "situacao", "teste"], primeira.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Outubro | Serviço Y", primeira.GetProperty("campanha").GetProperty("nome").GetString());
        Assert.Equal("criado", primeira.GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task O_historico_mostra_quem_mudou_e_quando_e_a_reabertura_pelo_sistema()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var atendente = await fabrica.LogarAsync(usuario);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await atendente.CriarLeadIdAsync(corpo);
        var antes = DateTimeOffset.UtcNow;
        await atendente.MudarEtapaAsync(leadId, (await fabrica.EtapaAsync("Perdido")).Id, await fabrica.MotivoPerdaIdAsync("Sem interesse"), "Comprou de outro");
        var depois = DateTimeOffset.UtcNow;
        using var n8n = fabrica.CriarClienteIntegracao();
        var novaEntrada = LeadsDeTeste.CorpoIntegracao();
        novaEntrada["email"] = corpo["email"];
        await n8n.EnviarLeadIdAsync(novaEntrada);

        var itens = await atendente.LinhaDoTempoAsync(leadId);

        Assert.Equal(["etapa", "entrada", "etapa", "entrada"], itens.Select(Tipo));
        Assert.Equal(("Perdido", "Novo"), Etapas(itens[0]));
        Assert.Null(NomeDoUsuario(itens[0]));

        var perda = itens[2];
        Assert.Equal(("Novo", "Perdido"), Etapas(perda));
        Assert.Equal(usuario.Nome, NomeDoUsuario(perda));
        Assert.InRange(perda.GetProperty("ocorridoEm").GetDateTimeOffset(), antes.AddMilliseconds(-1), depois);
        Assert.Equal("Sem interesse", perda.GetProperty("etapa").GetProperty("motivoPerda").GetProperty("nome").GetString());
        Assert.Equal("Comprou de outro", perda.GetProperty("etapa").GetProperty("motivoPerdaDetalhe").GetString());
    }

    [Fact]
    public async Task Troca_de_responsavel_vem_da_auditoria()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var nova = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarAsync(eu);
        using var n8n = fabrica.CriarClienteIntegracao();
        var leadId = await n8n.EnviarLeadIdAsync(LeadsDeTeste.CorpoIntegracao());
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            escopo.ServiceProvider.GetRequiredService<AuditoriaService>().Registrar(
                AcoesAuditoria.LeadReatribuido,
                EntidadesAuditoria.Lead,
                leadId.ToString(CultureInfo.InvariantCulture),
                new { anteriorId = (Guid?)null, novoId = nova.Id },
                eu.Id);
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().SaveChangesAsync(Ct);
        }

        var troca = (await admin.LinhaDoTempoAsync(leadId))[0];

        Assert.Equal("responsavel", Tipo(troca));
        Assert.Equal(eu.Nome, NomeDoUsuario(troca));
        Assert.Equal(JsonValueKind.Null, troca.GetProperty("responsavel").GetProperty("anterior").ValueKind);
        Assert.Equal(nova.Nome, troca.GetProperty("responsavel").GetProperty("nova").GetProperty("nome").GetString());
    }
}
