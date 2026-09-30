using System.Text.Json;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>Notificações para os testes: a lista pela API e o que está no banco.</summary>
public static class NotificacoesDeTeste
{
    /// <summary><c>GET /api/notificacoes</c> do usuário logado: <c>naoLidas</c> e <c>itens</c>.</summary>
    public static async Task<JsonElement> NotificacoesAsync(this HttpClient cliente)
    {
        var resposta = await cliente.GetAsync("/api/notificacoes", TestContext.Current.CancellationToken);
        resposta.EnsureSuccessStatusCode();
        return await resposta.LerJsonAsync();
    }

    public static List<JsonElement> Itens(this JsonElement notificacoes) => [.. notificacoes.GetProperty("itens").EnumerateArray()];

    /// <summary>As notificações do usuário no banco (de um lead, se informado), da primeira para a última.</summary>
    public static async Task<List<Notificacao>> NotificacoesDoUsuarioAsync(
        this WebApplicationFactory<Program> fabrica, Guid usuarioId, long? leadId = null)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId && (leadId == null || n.LeadId == leadId))
            .OrderBy(n => n.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Grava uma notificação pelo <see cref="Notificador"/>, como os outros módulos fazem.</summary>
    public static async Task NotificarAsync(
        this WebApplicationFactory<Program> fabrica,
        Guid usuarioId,
        long? leadId = null,
        TipoNotificacao tipo = TipoNotificacao.LeadAtribuido,
        string titulo = "Novo lead para você")
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        escopo.ServiceProvider.GetRequiredService<Notificador>().ParaUsuario(usuarioId, tipo, titulo, leadId, autorId: null);
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Passa o lead para outra pessoa direto no banco, como se um admin o tivesse trocado.</summary>
    public static async Task PassarParaAsync(this WebApplicationFactory<Program> fabrica, long leadId, Guid? responsavelId)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Leads
            .Where(l => l.Id == leadId)
            .ExecuteUpdateAsync(l => l.SetProperty(x => x.ResponsavelId, responsavelId), TestContext.Current.CancellationToken);
    }
}
