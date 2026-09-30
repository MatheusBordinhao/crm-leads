using System.Net;
using System.Text.Json;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests;

/// <summary>Notificações no sistema (seção 3.6). A visibilidade por atendente está em LeadsVisibilidadeTests.</summary>
public class NotificacoesTests(CrmApiFactory fabrica)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lista_so_as_do_usuario_da_mais_recente_para_a_mais_antiga_com_o_nome_atual_do_lead()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var pietraUsuario = await fabrica.CriarUsuarioAsync();
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        using var pietra = await fabrica.LogarAsync(pietraUsuario);
        var leadId = await adriana.CriarLeadIdAsync(LeadsDeTeste.CorpoLead("Nome Antigo"));
        await fabrica.NotificarAsync(adrianaUsuario.Id, titulo: "Primeira");
        await fabrica.NotificarAsync(adrianaUsuario.Id, leadId, TipoNotificacao.NovaEntrada, "Segunda");
        await fabrica.NotificarAsync(pietraUsuario.Id, titulo: "Da Pietra");
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            (await db.Leads.FindAsync([leadId], Ct))!.Nome = "Nome Novo";
            await db.SaveChangesAsync(Ct);
        }

        var lista = await adriana.NotificacoesAsync();

        Assert.Equal(2, lista.GetProperty("naoLidas").GetInt32());
        var itens = lista.Itens();
        Assert.Equal(["Segunda", "Primeira"], itens.Select(i => i.GetProperty("titulo").GetString()));
        Assert.Equal("nova_entrada", itens[0].GetProperty("tipo").GetString());
        Assert.Equal(leadId, itens[0].GetProperty("lead").GetProperty("id").GetInt64());
        Assert.Equal("Nome Novo", itens[0].GetProperty("lead").GetProperty("nome").GetString());
        Assert.Equal(JsonValueKind.Null, itens[1].GetProperty("lead").ValueKind);
        Assert.Equal(JsonValueKind.Null, itens[1].GetProperty("lidaEm").ValueKind);
        Assert.Equal(["Da Pietra"], (await pietra.NotificacoesAsync()).Itens().Select(i => i.GetProperty("titulo").GetString()));
    }

    [Fact]
    public async Task Mostra_as_30_mais_recentes_e_conta_todas_as_nao_lidas()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = await fabrica.LogarAsync(usuario);
        for (var i = 1; i <= NotificacoesController.Quantidade + 1; i++)
        {
            await fabrica.NotificarAsync(usuario.Id, titulo: $"Notificação {i}");
        }

        var lista = await cliente.NotificacoesAsync();

        Assert.Equal(NotificacoesController.Quantidade + 1, lista.GetProperty("naoLidas").GetInt32());
        var titulos = lista.Itens().Select(i => i.GetProperty("titulo").GetString()).ToList();
        Assert.Equal(NotificacoesController.Quantidade, titulos.Count);
        Assert.Equal($"Notificação {NotificacoesController.Quantidade + 1}", titulos[0]);
        Assert.DoesNotContain("Notificação 1", titulos);
    }

    [Fact]
    public async Task Marcar_uma_como_lida_e_depois_todas_zera_o_contador()
    {
        var usuario = await fabrica.CriarUsuarioAsync();
        using var cliente = await fabrica.LogarAsync(usuario);
        await fabrica.NotificarAsync(usuario.Id, titulo: "Primeira");
        await fabrica.NotificarAsync(usuario.Id, titulo: "Segunda");
        await fabrica.NotificarAsync(usuario.Id, titulo: "Terceira");
        var primeira = (await cliente.NotificacoesAsync()).Itens().Last().GetProperty("id").GetInt64();

        var umaLida = await cliente.PostAsync($"/api/notificacoes/{primeira}/lida", null, Ct);
        var depoisDeUma = await cliente.NotificacoesAsync();
        var todasLidas = await cliente.PostAsync("/api/notificacoes/lidas", null, Ct);
        var depoisDeTodas = await cliente.NotificacoesAsync();
        var lidaDeNovo = await cliente.PostAsync($"/api/notificacoes/{primeira}/lida", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, umaLida.StatusCode);
        Assert.Equal(2, depoisDeUma.GetProperty("naoLidas").GetInt32());
        Assert.Equal(JsonValueKind.String, depoisDeUma.Itens().Last().GetProperty("lidaEm").ValueKind);
        Assert.Equal(HttpStatusCode.NoContent, todasLidas.StatusCode);
        Assert.Equal(0, depoisDeTodas.GetProperty("naoLidas").GetInt32());
        Assert.All(depoisDeTodas.Itens(), i => Assert.Equal(JsonValueKind.String, i.GetProperty("lidaEm").ValueKind));
        Assert.Equal(HttpStatusCode.NoContent, lidaDeNovo.StatusCode);
    }

    [Fact]
    public async Task Cadastro_a_mao_de_quem_ja_e_lead_de_outra_avisa_a_responsavel_e_os_admins_e_nao_quem_cadastrou()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var pietraUsuario = await fabrica.CriarUsuarioAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        using var pietra = await fabrica.LogarAsync(pietraUsuario);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await adriana.CriarLeadIdAsync(corpo);
        var repetido = LeadsDeTeste.CorpoLead("Outro Nome");
        repetido["email"] = corpo["email"];

        var resposta = await pietra.CriarLeadAsync(repetido);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var daResponsavel = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(adrianaUsuario.Id, leadId));
        Assert.Equal(TipoNotificacao.NovaEntrada, daResponsavel.Tipo);
        Assert.Equal($"{pietraUsuario.Nome} cadastrou à mão uma pessoa que já é um lead seu", daResponsavel.Titulo);
        var doAdmin = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Equal(TipoNotificacao.CadastroRepetido, doAdmin.Tipo);
        Assert.Equal($"{pietraUsuario.Nome} cadastrou à mão uma pessoa que já é lead de {adrianaUsuario.Nome}", doAdmin.Titulo);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(pietraUsuario.Id));
        Assert.Equal(0, (await pietra.NotificacoesAsync()).GetProperty("naoLidas").GetInt32());
    }

    [Fact]
    public async Task Nova_entrada_da_integracao_avisa_a_responsavel()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await adriana.CriarLeadIdAsync(corpo);
        using var n8n = fabrica.CriarClienteIntegracao();
        var entrada = LeadsDeTeste.CorpoIntegracao();
        entrada["email"] = corpo["email"];

        await n8n.EnviarLeadIdAsync(entrada);

        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(adrianaUsuario.Id, leadId));
        Assert.Equal(TipoNotificacao.NovaEntrada, aviso.Tipo);
        Assert.Equal("Nova entrada em um lead seu", aviso.Titulo);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
    }

    [Fact]
    public async Task Admin_que_cadastra_para_uma_atendente_avisa_so_ela()
    {
        var atendente = await fabrica.CriarUsuarioAsync();
        var eu = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var admin = await fabrica.LogarAsync(eu);
        var corpo = LeadsDeTeste.CorpoLead();
        corpo["responsavelId"] = atendente.Id;

        var leadId = await admin.CriarLeadIdAsync(corpo);

        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(atendente.Id, leadId));
        Assert.Equal(TipoNotificacao.LeadAtribuido, aviso.Tipo);
        Assert.Equal($"{eu.Nome} cadastrou um lead para você", aviso.Titulo);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(eu.Id, leadId));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(outroAdmin.Id, leadId));
    }

    [Fact]
    public async Task Ninguem_e_avisado_do_que_a_propria_atendente_cadastra()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        var corpo = LeadsDeTeste.CorpoLead();

        var leadId = await adriana.CriarLeadIdAsync(corpo);
        var repetido = await adriana.CriarLeadAsync(new Dictionary<string, object?>(corpo) { ["nome"] = "De novo" });

        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(adrianaUsuario.Id));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
    }

    [Fact]
    public async Task Entrada_repetida_em_lead_sem_responsavel_avisa_os_admins()
    {
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var atendente = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await atendente.CriarLeadIdAsync(corpo);
        await fabrica.PassarParaAsync(leadId, null);
        using var n8n = fabrica.CriarClienteIntegracao();
        var entrada = LeadsDeTeste.CorpoIntegracao();
        entrada["email"] = corpo["email"];

        await n8n.EnviarLeadIdAsync(entrada);

        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Equal(TipoNotificacao.NovaEntrada, aviso.Tipo);
        Assert.Equal("Nova entrada em um lead sem responsável", aviso.Titulo);
    }

    [Fact]
    public async Task Responsavel_desativada_conta_como_sem_responsavel()
    {
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        var corpo = LeadsDeTeste.CorpoLead();
        var leadId = await adriana.CriarLeadIdAsync(corpo);
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
            (await db.Users.FindAsync([adrianaUsuario.Id], Ct))!.Ativo = false;
            await db.SaveChangesAsync(Ct);
        }

        using var n8n = fabrica.CriarClienteIntegracao();
        var entrada = LeadsDeTeste.CorpoIntegracao();
        entrada["email"] = corpo["email"];

        await n8n.EnviarLeadIdAsync(entrada);

        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(adrianaUsuario.Id));
        var aviso = Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        Assert.Equal($"Nova entrada no lead de {adrianaUsuario.Nome} (usuário desativado)", aviso.Titulo);
    }

    [Fact]
    public async Task Lead_de_teste_nao_gera_notificacao()
    {
        var admin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var n8n = fabrica.CriarClienteIntegracao();
        var corpo = LeadsDeTeste.CorpoIntegracao();
        corpo["teste"] = true;

        var leadId = await n8n.EnviarLeadIdAsync(corpo);
        await n8n.EnviarLeadIdAsync(new Dictionary<string, object?>(corpo) { ["origem"] = "site", ["idExterno"] = LeadsDeTeste.IdExternoUnico() });

        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(admin.Id, leadId));
        await using var escopo = fabrica.Services.CreateAsyncScope();
        Assert.False(await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Notificacoes.AnyAsync(n => n.LeadId == leadId, Ct));
    }

    [Fact]
    public async Task Os_titulos_nao_levam_dados_do_lead()
    {
        var marca = Guid.NewGuid().ToString("N")[..10];
        var adrianaUsuario = await fabrica.CriarUsuarioAsync();
        await fabrica.CriarUsuarioAsync(Perfil.Admin);
        using var adriana = await fabrica.LogarAsync(adrianaUsuario);
        using var pietra = await fabrica.LogarComoAsync(Perfil.Atendente);
        var corpo = LeadsDeTeste.CorpoLead($"Nome Sigiloso {marca}");
        corpo["empresa"] = $"Empresa Sigilosa {marca}";
        var leadId = await adriana.CriarLeadIdAsync(corpo);
        using var n8n = fabrica.CriarClienteIntegracao();
        var entrada = LeadsDeTeste.CorpoIntegracao();
        entrada["email"] = corpo["email"];
        entrada["nome"] = corpo["nome"];
        await n8n.EnviarLeadIdAsync(entrada);
        await pietra.CriarLeadAsync(corpo);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var titulos = await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Notificacoes
            .Where(n => n.LeadId == leadId)
            .Select(n => n.Titulo)
            .ToListAsync(Ct);
        Assert.NotEmpty(titulos);
        Assert.All(titulos, t => Assert.DoesNotContain(marca, t, StringComparison.Ordinal));
        Assert.All(titulos, t => Assert.DoesNotContain((string)corpo["email"]!, t, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Os_admins_ativos_sao_avisados_menos_o_autor_da_acao()
    {
        var autor = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var adminDesativado = await fabrica.CriarUsuarioAsync(Perfil.Admin, ativo: false);
        var atendente = await fabrica.CriarUsuarioAsync();

        // Um título só deste teste: os admins ativos também recebem os avisos dos testes que rodam junto.
        var titulo = $"Falha no envio ao n8n {Guid.NewGuid():N}";
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<Notificador>()
                .ParaAdminsAsync(TipoNotificacao.EventoFalhou, titulo, leadId: null, autorId: autor.Id, Ct);
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().SaveChangesAsync(Ct);
        }

        async Task<int> QuantasAsync(Guid usuarioId) =>
            (await fabrica.NotificacoesDoUsuarioAsync(usuarioId)).Count(n => n.Titulo == titulo);
        Assert.Equal(1, await QuantasAsync(outroAdmin.Id));
        Assert.Equal(0, await QuantasAsync(autor.Id));
        Assert.Equal(0, await QuantasAsync(adminDesativado.Id));
        Assert.Equal(0, await QuantasAsync(atendente.Id));
    }
}
