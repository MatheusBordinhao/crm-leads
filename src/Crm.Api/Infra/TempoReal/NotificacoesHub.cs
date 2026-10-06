using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.TempoReal;

/// <summary>
/// Os avisos que o hub manda para a tela (seção 3.6). Levam só ids: a tela busca os dados na API, que aplica a
/// visibilidade de sempre, e nenhum dado pessoal passa pelo hub.
/// </summary>
public interface IClienteTempoReal
{
    /// <summary>
    /// Esses leads mudaram. Sem ids, quer dizer "recarregue o que estiver aberto": um lead da pessoa passou para outra,
    /// e o id dele não vai para quem não o vê mais.
    /// </summary>
    Task LeadsAlterados(long[] ids);

    /// <summary>Uma notificação nova para a pessoa.</summary>
    Task NotificacaoNova(long id);

    /// <summary>As etapas ou os motivos de perda mudaram.</summary>
    Task FunilAlterado();
}

/// <summary>Os grupos do hub: um por usuário e um dos admins.</summary>
public static class GruposTempoReal
{
    public const string Admins = "admins";

    public static string DoUsuario(Guid usuarioId) => $"usuario:{usuarioId}";
}

/// <summary>
/// Hub do tempo real, em <c>/hubs/notificacoes</c>. Só avisa: o cliente não tem métodos para chamar. Como o resto da
/// API, exige sessão e senha definitiva. Ao conectar, o usuário é conferido no banco, como a sessão a cada minuto
/// (<see cref="ConexoesTempoReal"/>): o cookie pode estar com até um minuto de atraso. O grupo dos admins vem do perfil
/// gravado no banco, e não do cookie.
/// </summary>
[Authorize]
public sealed class NotificacoesHub(CrmDbContext db, ConexoesTempoReal conexoes, IOptions<IdentityOptions> identidade)
    : Hub<IClienteTempoReal>
{
    public const string Rota = "/hubs/notificacoes";

    public override async Task OnConnectedAsync()
    {
        var ct = Context.ConnectionAborted;
        var carimbo = Context.User?.FindFirst(identidade.Value.ClaimsIdentity.SecurityStampClaimType)?.Value;
        var situacao = Guid.TryParse(Context.UserIdentifier, out var usuarioId)
            ? (await db.SituacoesAsync([usuarioId], ct)).SingleOrDefault()
            : null;
        if (situacao is null || !situacao.Aceita(carimbo))
        {
            // Sessão que já não vale: a tela confere a sessão antes de conectar de novo e, com ela encerrada, vai para o login.
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GruposTempoReal.DoUsuario(usuarioId), ct);
        if (situacao.EhAdmin)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GruposTempoReal.Admins, ct);
        }

        conexoes.Registrar(new ConexaoAberta(Context, usuarioId, carimbo, situacao.EhAdmin));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        conexoes.Remover(Context.ConnectionId);
        return Task.CompletedTask;
    }
}
