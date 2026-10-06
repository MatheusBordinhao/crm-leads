using System.Data.Common;
using System.Runtime.CompilerServices;
using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Api.Infra.TempoReal;

/// <summary>
/// Anota o que cada <c>SaveChanges</c> grava e interessa às telas e entrega à <see cref="FilaTempoReal"/> só depois do
/// commit. Assim, nenhum ponto do código precisa lembrar de avisar (a integração, o cadastro, a importação, o
/// despachante), e um aviso nunca sai de uma transação que voltou atrás. Regras, conferidas no código do EF Core 10:
/// <list type="bullet">
/// <item>a anotação é refeita a cada <c>SavingChanges</c>, depois de um <c>DetectChanges</c> (o EF só faz o dele depois
/// dos interceptadores), e só vale no <c>SavedChanges</c>: um <c>SaveChanges</c> que falha não avisa, inclusive no
/// conflito de versão, que nem chama o <c>SaveChangesFailed</c>;</item>
/// <item>fora de transação, entrega no <c>SavedChanges</c>; dentro de uma, guarda e entrega no commit, num aviso só;</item>
/// <item>o <c>TransactionStarted</c> descarta só o que sobrou de uma transação que não foi confirmada, nunca a anotação em
/// andamento: sem transação no código, o próprio EF abre uma dentro do <c>SaveChanges</c>;</item>
/// <item>rollback ou falha no commit descartam o que estava guardado. O contexto é por escopo, sem pool: o que sobrar de
/// uma transação descartada sem commit some com ele.</item>
/// </list>
/// O que não passa pelo <c>SaveChanges</c> (<c>ExecuteUpdate</c>, SQL direto) fica de fora.
/// </summary>
internal sealed class InterceptadorTempoReal(FilaTempoReal fila) : ISaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly ConditionalWeakTable<DbContext, Estado> _estados = new();

    public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Anotar(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Anotar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Confirmar(eventData.Context);
        return result;
    }

    public ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Confirmar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public DbTransaction TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Descartar(eventData.Context);
        return result;
    }

    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) => Entregar(eventData.Context);

    public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Entregar(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) => Descartar(eventData.Context);

    public Task TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) => Descartar(eventData.Context);

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    private void Anotar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        contexto.ChangeTracker.DetectChanges();
        var anotacao = new Anotacao();
        foreach (var entrada in contexto.ChangeTracker.Entries())
        {
            if (entrada.State is EntityState.Unchanged or EntityState.Detached)
            {
                continue;
            }

            var nova = entrada.State == EntityState.Added;
            switch (entrada.Entity)
            {
                case Lead lead:
                    anotacao.Leads.Add(lead);
                    if (entrada.State == EntityState.Modified
                        && entrada.Property(nameof(Lead.ResponsavelId)).OriginalValue is Guid anterior
                        && anterior != lead.ResponsavelId)
                    {
                        anotacao.Trocas.Add(new TrocaDeResponsavel(lead.Id, anterior));
                    }

                    break;
                case Interacao interacao when nova:
                    anotacao.OutrosLeads.Add(interacao.LeadId);
                    break;
                case HistoricoEtapa historico when nova:
                    anotacao.OutrosLeads.Add(historico.LeadId);
                    break;
                case EntradaRecebida { LeadId: { } leadId } when nova:
                    anotacao.OutrosLeads.Add(leadId);
                    break;
                case Notificacao notificacao when nova:
                    anotacao.Notificacoes.Add(notificacao);
                    break;
                case Etapa or MotivoPerda:
                    anotacao.Funil = true;
                    break;
            }
        }

        _estados.GetOrCreateValue(contexto).Atual = anotacao.Vazia ? null : anotacao;
    }

    private void Confirmar(DbContext? contexto)
    {
        if (contexto is null || !_estados.TryGetValue(contexto, out var estado) || estado.Atual is not { } anotacao)
        {
            return;
        }

        estado.Atual = null;
        var mudancas = anotacao.Resolver();
        if (contexto.Database.CurrentTransaction is not null)
        {
            estado.Guardadas.Add(mudancas);
            return;
        }

        // Fora de transação, o SaveChanges já está no banco. O que estava guardado é de uma transação descartada sem commit.
        estado.Guardadas.Clear();
        fila.Publicar(mudancas);
    }

    private void Entregar(DbContext? contexto)
    {
        if (contexto is null || !_estados.TryGetValue(contexto, out var estado) || estado.Guardadas.Count == 0)
        {
            return;
        }

        var mudancas = MudancasTempoReal.Juntar(estado.Guardadas);
        estado.Guardadas.Clear();
        fila.Publicar(mudancas);
    }

    private void Descartar(DbContext? contexto)
    {
        if (contexto is not null && _estados.TryGetValue(contexto, out var estado))
        {
            estado.Guardadas.Clear();
        }
    }

    /// <summary>O que está anotado num contexto: o <c>SaveChanges</c> em andamento e os da transação aberta.</summary>
    private sealed class Estado
    {
        public Anotacao? Atual { get; set; }

        public List<MudancasTempoReal> Guardadas { get; } = [];
    }

    /// <summary>O que um <c>SaveChanges</c> vai gravar. Os leads novos só ganham o id depois dele.</summary>
    private sealed class Anotacao
    {
        public List<Lead> Leads { get; } = [];

        /// <summary>Leads que não mudaram, mas ganharam uma interação, uma entrada ou uma mudança de etapa.</summary>
        public List<long> OutrosLeads { get; } = [];

        public List<TrocaDeResponsavel> Trocas { get; } = [];

        public List<Notificacao> Notificacoes { get; } = [];

        public bool Funil { get; set; }

        public bool Vazia => Leads.Count == 0 && OutrosLeads.Count == 0 && Notificacoes.Count == 0 && !Funil;

        /// <summary>As mudanças, com os ids que o banco gerou no <c>SaveChanges</c>.</summary>
        public MudancasTempoReal Resolver() => new(
            [.. Leads.Select(l => l.Id).Concat(OutrosLeads).Distinct()],
            Trocas,
            [.. Notificacoes.Select(n => new NotificacaoGravada(n.Id, n.UsuarioId))],
            Funil);
    }
}
