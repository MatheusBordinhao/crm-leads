using System.Net;
using System.Text.Json;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Dados;
using Crm.IntegrationTests.Infra;
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
    public async Task Os_admins_ativos_sao_avisados_menos_o_autor_da_acao()
    {
        var autor = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var outroAdmin = await fabrica.CriarUsuarioAsync(Perfil.Admin);
        var adminDesativado = await fabrica.CriarUsuarioAsync(Perfil.Admin, ativo: false);
        var atendente = await fabrica.CriarUsuarioAsync();

        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<Notificador>()
                .ParaAdminsAsync(TipoNotificacao.EventoFalhou, "Falha no envio ao n8n", leadId: null, autorId: autor.Id, Ct);
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().SaveChangesAsync(Ct);
        }

        Assert.Single(await fabrica.NotificacoesDoUsuarioAsync(outroAdmin.Id));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(autor.Id));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(adminDesativado.Id));
        Assert.Empty(await fabrica.NotificacoesDoUsuarioAsync(atendente.Id));
    }
}
