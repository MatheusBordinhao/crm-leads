using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Rodízio só com as pessoas do teste. O rodízio vale para todos: quem participa dele recebe os leads que os
/// outros testes mandam. Por isso, teste que põe alguém no rodízio fica na coleção <see cref="EstadoGlobal"/> e
/// usa este ajuste, que tira do rodízio quem já estava, liga ou desliga a distribuição e, no fim, devolve tudo
/// como estava.
/// </summary>
public sealed class RodizioIsolado : IAsyncDisposable
{
    private readonly CrmApiFactory _fabrica;
    private readonly List<Guid> _estavamNoRodizio;
    private readonly string _distribuicaoAntes;
    private readonly List<Guid> _participantes = [];

    private RodizioIsolado(CrmApiFactory fabrica, List<Guid> estavamNoRodizio, string distribuicaoAntes)
    {
        _fabrica = fabrica;
        _estavamNoRodizio = estavamNoRodizio;
        _distribuicaoAntes = distribuicaoAntes;
    }

    public static async Task<RodizioIsolado> PrepararAsync(CrmApiFactory fabrica, bool distribuicaoAutomatica = true)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        var estavam = await db.Users.Where(u => u.ParticipaRodizio).Select(u => u.Id).ToListAsync(ct);
        await db.Users.Where(u => u.ParticipaRodizio).ExecuteUpdateAsync(u => u.SetProperty(x => x.ParticipaRodizio, false), ct);
        var antes = await DefinirDistribuicaoAsync(db, distribuicaoAutomatica ? "true" : "false", ct);
        return new RodizioIsolado(fabrica, estavam, antes);
    }

    /// <summary>Uma atendente no rodízio, que recebeu lead pelo rodízio em <paramref name="ultimoLead"/> (nulo: nunca).</summary>
    public async Task<Usuario> AdicionarAsync(DateTimeOffset? ultimoLead = null)
    {
        var usuario = await _fabrica.CriarUsuarioAsync();
        await using (var escopo = _fabrica.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<CrmDbContext>().Users
                .Where(u => u.Id == usuario.Id)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(x => x.ParticipaRodizio, true).SetProperty(x => x.UltimoLeadAtribuidoEm, ultimoLead),
                    TestContext.Current.CancellationToken);
        }

        _participantes.Add(usuario.Id);
        return await _fabrica.BuscarUsuarioAsync(usuario.Id);
    }

    /// <summary>Quem entrou no rodízio de outro jeito (pela API, por exemplo) também sai dele no fim do teste.</summary>
    public void Acompanhar(Guid usuarioId) => _participantes.Add(usuarioId);

    public async ValueTask DisposeAsync()
    {
        // Sem o token do teste: o estado volta mesmo quando o teste é cancelado.
        await using var escopo = _fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<CrmDbContext>();
        await db.Users.Where(u => _participantes.Contains(u.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.ParticipaRodizio, false));
        await db.Users.Where(u => _estavamNoRodizio.Contains(u.Id)).ExecuteUpdateAsync(u => u.SetProperty(x => x.ParticipaRodizio, true));
        await DefinirDistribuicaoAsync(db, _distribuicaoAntes, CancellationToken.None);
    }

    /// <summary>Grava a configuração e devolve o valor que estava.</summary>
    private static async Task<string> DefinirDistribuicaoAsync(CrmDbContext db, string valor, CancellationToken ct)
    {
        var configuracao = await db.Configuracoes.SingleAsync(c => c.Chave == ChavesConfiguracao.DistribuicaoAutomatica, ct);
        var antes = configuracao.Valor;
        configuracao.Valor = valor;
        await db.SaveChangesAsync(ct);
        return antes;
    }
}
