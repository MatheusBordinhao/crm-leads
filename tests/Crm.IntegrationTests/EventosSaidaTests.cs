using System.Net.Http.Json;
using System.Text.Json;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Eventos;
using Crm.IntegrationTests.Infra;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>
/// Eventos para o n8n (seção 4.3), gravados na mesma transação da mudança. A entrega está em
/// DespachanteEventosTests.
/// </summary>
public class EventosSaidaTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lead_novo_da_integracao_gera_o_evento_da_secao_4_3_sem_email_telefone_nem_documento()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["cpfCnpj"] = LeadsDeTeste.CpfUnico();
        var leadId = await n8n.EnviarLeadIdAsync(corpo);

        var evento = Assert.Single(await fabrica.EventosDoLeadAsync(leadId));

        Assert.Equal(TipoEvento.LeadCriado, evento.Tipo);
        Assert.Equal(SituacaoEvento.Pendente, evento.Situacao);
        Assert.Equal(0, evento.Tentativas);
        Assert.True(evento.ProximaTentativaEm <= DateTimeOffset.UtcNow);
        var json = evento.Json();
        Assert.Equal(evento.Id, json.GetProperty("id").GetGuid());
        Assert.Equal("lead.criado", json.GetProperty("tipo").GetString());
        // O banco guarda até o microssegundo.
        Assert.Equal(evento.CriadoEm, json.GetProperty("ocorridoEm").GetDateTimeOffset(), TimeSpan.FromMilliseconds(1));
        var lead = json.GetProperty("lead");
        Assert.Equal(leadId, lead.GetProperty("id").GetInt64());
        Assert.Equal("Maria Souza", lead.GetProperty("nome").GetString());
        Assert.Equal("Empresa X", lead.GetProperty("empresa").GetString());
        Assert.Equal("RS", lead.GetProperty("uf").GetString());
        Assert.Equal("meta_lead_ads", lead.GetProperty("origem").GetString());
        Assert.Equal("instagram", lead.GetProperty("plataforma").GetString());
        Assert.Equal("Outubro | Serviço Y", lead.GetProperty("campanha").GetString());
        Assert.Equal((string)corpo["idExterno"]!, lead.GetProperty("idExterno").GetString());
        Assert.Equal("Novo", lead.GetProperty("etapa").GetProperty("nome").GetString());
        Assert.Equal("aberta", lead.GetProperty("etapa").GetProperty("tipo").GetString());
        Assert.Equal($"{CrmApiFactory.UrlPublica}/leads/{leadId}", lead.GetProperty("url").GetString());
        Assert.Empty(json.GetProperty("dados").EnumerateObject());

        var texto = json.GetRawText();
        Assert.DoesNotContain((string)corpo["email"]!, texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(((string)corpo["telefone"]!)[3..], texto, StringComparison.Ordinal);
        Assert.DoesNotContain((string)corpo["cpfCnpj"]!, texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lead_que_o_admin_passa_para_uma_atendente_leva_o_email_dela()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = atendente.Id;

        var leadId = await admin.CriarLeadIdAsync(corpo);

        var responsavel = Assert.Single(await fabrica.EventosDoLeadAsync(leadId)).Json().GetProperty("responsavel");
        Assert.Equal(atendente.Id, responsavel.GetProperty("id").GetGuid());
        Assert.Equal(atendente.Nome, responsavel.GetProperty("nome").GetString());
        Assert.Equal(atendente.Email, responsavel.GetProperty("email").GetString());
    }

    [Fact]
    public async Task O_que_a_propria_responsavel_faz_nao_vai_para_o_n8n()
    {
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead();

        var leadId = await atendente.CriarLeadIdAsync(corpo);
        await atendente.CriarLeadAsync(new Dictionary<string, object?>(corpo) { ["nome"] = "De novo" });

        Assert.Empty(await fabrica.EventosDoLeadAsync(leadId));
    }

    [Fact]
    public async Task Entrada_repetida_leva_a_origem_e_a_campanha_da_nova_entrada()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var primeira = LeadsDeTeste.CorpoIntegracao();
        var leadId = await n8n.EnviarLeadIdAsync(primeira);
        var repetida = LeadsDeTeste.CorpoIntegracao("site");
        repetida["email"] = primeira["email"];
        repetida["campanha"] = new { id = LeadsDeTeste.IdExternoUnico(), nome = "Site | Outubro" };

        await n8n.EnviarLeadIdAsync(repetida);

        var eventos = await fabrica.EventosDoLeadAsync(leadId);
        Assert.Equal([TipoEvento.LeadCriado, TipoEvento.LeadEntradaRepetida], eventos.Select(e => e.Tipo));
        var json = eventos[1].Json();
        Assert.Equal("lead.entrada_repetida", json.GetProperty("tipo").GetString());
        Assert.Equal("site", json.GetProperty("dados").GetProperty("origem").GetString());
        Assert.Equal("Site | Outubro", json.GetProperty("dados").GetProperty("campanha").GetString());
        Assert.Equal("meta_lead_ads", json.GetProperty("lead").GetProperty("origem").GetString());
        Assert.Equal((string)primeira["idExterno"]!, json.GetProperty("lead").GetProperty("idExterno").GetString());
    }

    [Fact]
    public async Task Reenvio_ja_recebido_nao_gera_outro_evento()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        var leadId = await n8n.EnviarLeadIdAsync(corpo);

        await n8n.EnviarLeadIdAsync(corpo);

        Assert.Single(await fabrica.EventosDoLeadAsync(leadId));
    }

    [Fact]
    public async Task Lead_de_teste_nao_gera_evento()
    {
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;

        var leadId = await n8n.EnviarLeadIdAsync(corpo);
        await n8n.EnviarLeadIdAsync(new Dictionary<string, object?>(corpo) { ["origem"] = "site", ["idExterno"] = LeadsDeTeste.IdExternoUnico() });

        Assert.Empty(await fabrica.EventosDoLeadAsync(leadId));
    }

    [Fact]
    public async Task Troca_de_responsavel_leva_a_responsavel_anterior()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var pietraUsuario = await fabrica.CriarUsuarioAsync();
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var leadId = await adriana.CriarLeadIdAsync();

        await admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { responsavelId = pietraUsuario.Id }, Ct);

        var json = Assert.Single(await fabrica.EventosDoLeadAsync(leadId)).Json();
        Assert.Equal("lead.reatribuido", json.GetProperty("tipo").GetString());
        Assert.Equal(pietraUsuario.Id, json.GetProperty("responsavel").GetProperty("id").GetGuid());
        var anterior = json.GetProperty("dados").GetProperty("responsavelAnterior");
        Assert.Equal(adrianaUsuario.Id, anterior.GetProperty("id").GetGuid());
        Assert.Equal(adrianaUsuario.Nome, anterior.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Admin_que_passa_o_lead_para_si_nao_gera_evento()
    {
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var leadId = await atendente.CriarLeadIdAsync();

        await admin.PostAsJsonAsync($"/api/leads/{leadId}/responsavel", new { responsavelId = eu.Id }, Ct);

        Assert.Empty(await fabrica.EventosDoLeadAsync(leadId));
    }

    [Fact]
    public async Task Responsavel_desativada_vai_nula_para_o_n8n_avisar_o_admin()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        using var admin = await fabrica.LogarComoAsync(Perfil.Admin);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = atendente.Id;
        var leadId = await admin.CriarLeadIdAsync(corpo);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            (await db.Users.FindAsync([atendente.Id], Ct))!.Ativo = false;
            await db.SaveChangesAsync(Ct);
        }

        using var n8n = fabrica.CriarClienteIntegracao();
        var entrada = LeadsDeTeste.CorpoIntegracao();
        entrada["email"] = corpo["email"];
        await n8n.EnviarLeadIdAsync(entrada);

        var repetida = (await fabrica.EventosDoLeadAsync(leadId)).Single(e => e.Tipo == TipoEvento.LeadEntradaRepetida);
        Assert.Equal(JsonValueKind.Null, repetida.Json().GetProperty("responsavel").ValueKind);
    }

    [Theory]
    [InlineData("N8n:WebhookUrl", "https://exemplo.app.n8n.cloud/webhook/crm-alertas", "N8n__WebhookChave")]
    [InlineData("N8n:Eventos", "lead.ganho", "N8n__Eventos tem um tipo desconhecido")]
    [InlineData("Crm:UrlPublica", "", "Crm__UrlPublica")]
    public void Configuracao_errada_impede_a_api_de_subir(string chave, string valor, string mensagem)
    {
        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting(chave, valor));

        var erro = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains(mensagem, erro.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mudanca_de_etapa_so_vai_para_o_n8n_quando_ligada_em_N8n__Eventos()
    {
        using var atendentePadrao = await fabrica.LogarComoAsync(Perfil.Atendente);
        var semEvento = await atendentePadrao.CriarLeadIdAsync();
        await atendentePadrao.MudarEtapaAsync(semEvento, (await fabrica.EtapaAsync("Em contato")).Id);

        using var api = fabrica.WithWebHostBuilder(builder => builder.UseSetting("N8n:Eventos", "lead.etapa_alterada"));
        var usuario = await api.CriarUsuarioAsync();
        using var atendente = await api.LogarAsync(usuario);
        var leadId = await atendente.CriarLeadIdAsync();
        var perdido = await api.EtapaAsync("Perdido");
        await atendente.MudarEtapaAsync(leadId, perdido.Id, await api.MotivoPerdaIdAsync("Sem resposta"));

        Assert.Empty(await fabrica.EventosDoLeadAsync(semEvento));
        var json = Assert.Single(await fabrica.EventosDoLeadAsync(leadId)).Json();
        Assert.Equal("lead.etapa_alterada", json.GetProperty("tipo").GetString());
        Assert.Equal(perdido.Id, json.GetProperty("lead").GetProperty("etapa").GetProperty("id").GetInt32());
        var anterior = json.GetProperty("dados").GetProperty("etapaAnterior");
        Assert.Equal("Novo", anterior.GetProperty("nome").GetString());
        Assert.Equal("aberta", anterior.GetProperty("tipo").GetString());
        Assert.Equal("Sem resposta", json.GetProperty("dados").GetProperty("motivoPerda").GetString());
    }
}
