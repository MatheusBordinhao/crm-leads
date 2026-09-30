using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Rodízio (seção 3.3): o lead novo vai para quem, entre os usuários ativos que participam do rodízio, recebeu
/// lead pelo rodízio há mais tempo. Só o rodízio mexe na vez (<c>ultimo_lead_atribuido_em</c>): a escolha de um
/// admin, a troca de responsável e o lead que a atendente cadastra para si não contam.
/// </summary>
public sealed class Distribuicao(CrmDbContext db, Configuracoes configuracoes, TimeProvider relogio)
{
    /// <summary>
    /// Escolhe a próxima pessoa da vez e passa a vez adiante. Nulo quando a distribuição automática está
    /// desligada ou ninguém participa do rodízio. A escolha roda sob uma trava global, até o fim da transação:
    /// dois leads simultâneos nunca caem na mesma pessoa. A trava é pega depois das travas da entrada, e nada
    /// mais é travado depois dela, então duas entradas nunca esperam uma pela outra.
    /// </summary>
    public async Task<Guid?> EscolherPeloRodizioAsync(CancellationToken ct)
    {
        if (!await configuracoes.DistribuicaoAutomaticaAsync(ct))
        {
            return null;
        }

        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended('rodizio', 0))", ct);

        // A hora é lida depois da trava: quem pega a trava depois grava uma hora maior, e a vez anda na ordem certa
        // mesmo com entradas simultâneas.
        var agora = relogio.GetUtcNow();
        var escolhido = await db.Users.NaOrdemDaVez().Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct);
        if (escolhido is { } id)
        {
            // Direto no banco, e não pelo SaveChanges: assim não esbarra no ConcurrencyStamp do Identity, que uma
            // edição do usuário feita ao mesmo tempo mudaria.
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.UltimoLeadAtribuidoEm, agora), ct);
        }

        return escolhido;
    }
}
