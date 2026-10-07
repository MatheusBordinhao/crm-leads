using Crm.Api.Features.Funil;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Painel;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Demonstracao;

/// <param name="Preparada">Falso quando uma das condições não vale: nesse caso, nada mudou.</param>
/// <param name="Mensagem">O que aconteceu, só com contagens, para o terminal.</param>
public sealed record ResultadoDemonstracao(bool Preparada, string Mensagem);

/// <summary>
/// Prepara a demonstração do painel (README › Demonstração): apaga os leads e tudo o que depende deles, as campanhas e
/// as importações, e grava a massa fictícia (<see cref="MassaDemonstracao"/>). Usuários, etapas, motivos, configurações
/// e auditoria ficam como estão; da vez do rodízio, só quem é o próximo muda. Tudo numa transação: se algo falhar,
/// nada muda.
/// </summary>
/// <remarks>
/// A massa é gravada direto, sem o <see cref="ReceberEntrada"/> nem o <see cref="MudancaEtapa"/>: não sai notificação
/// nem evento para o n8n.
/// </remarks>
public sealed class PreparacaoDemonstracao(
    CrmDbContext db,
    IConfiguration configuracao,
    IOptions<CrmOptions> crm,
    AuditoriaService auditoria,
    TimeProvider relogio)
{
    /// <summary>A configuração que libera o comando: a variável de ambiente <c>Demo__Permitir</c>, só no comando.</summary>
    public const string ChavePermissao = "Demo:Permitir";

    public const string MensagemSemPermissao =
        "A preparação da demonstração apaga todos os leads. Para rodar, defina Demo__Permitir=true só no comando, nunca no .env.";

    public const string MensagemForaDeLocalhost =
        "A preparação da demonstração só roda num CRM local, com o Crm__UrlPublica em localhost.";

    public const string MensagemSemAtendentes = "Cadastre pelo menos uma atendente ativa antes de preparar a demonstração.";

    public async Task<ResultadoDemonstracao> ExecutarAsync(CancellationToken ct)
    {
        if (!string.Equals(configuracao[ChavePermissao], "true", StringComparison.OrdinalIgnoreCase))
        {
            return new ResultadoDemonstracao(false, MensagemSemPermissao);
        }

        if (!Uri.TryCreate(crm.Value.UrlPublica, UriKind.Absolute, out var url) || !url.IsLoopback)
        {
            return new ResultadoDemonstracao(false, MensagemForaDeLocalhost);
        }

        var atendentes = EmOrdemAlfabetica(await db.Atendentes().Where(u => u.Ativo).Select(u => new { u.Id, u.Nome }).ToListAsync(ct), a => a.Nome, a => a.Id);
        if (atendentes.Count == 0)
        {
            return new ResultadoDemonstracao(false, MensagemSemAtendentes);
        }

        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var leadsApagados = await ApagarAsync(ct);

        var campanhas = MassaDemonstracao.Campanhas.ToDictionary(
            c => c.Chave, c => new Campanha { Origem = c.Origem, IdExterno = c.IdExterno, Nome = c.Nome });
        db.Campanhas.AddRange(campanhas.Values);
        await db.SaveChangesAsync(ct);

        var agora = relogio.GetUtcNow();
        var etapas = await db.Etapas.AsNoTracking().Where(e => e.Ativa).OrderBy(e => e.Ordem).ThenBy(e => e.Id).ToListAsync(ct);
        var motivos = await db.MotivosPerda.AsNoTracking().Where(m => m.Ativo).OrderBy(m => m.Id).ToListAsync(ct);
        var massa = MassaDemonstracao.Gerar(new ContextoDemonstracao(
            agora,
            [.. etapas.Where(e => e.Tipo == TipoEtapa.Aberta)],
            etapas.Single(e => e.Tipo == TipoEtapa.Ganho),
            etapas.Single(e => e.Tipo == TipoEtapa.Perdido),
            motivos,
            atendentes,
            campanhas));

        // Os leads primeiro, para as entradas, as interações e o histórico ganharem o id deles.
        db.Leads.AddRange(massa.Select(m => m.Lead));
        await db.SaveChangesAsync(ct);
        foreach (var (lead, entradas, interacoes, historico) in massa)
        {
            foreach (var entrada in entradas)
            {
                entrada.LeadId = lead.Id;
            }

            foreach (var interacao in interacoes)
            {
                interacao.LeadId = lead.Id;
            }

            foreach (var mudanca in historico)
            {
                mudanca.LeadId = lead.Id;
            }

            db.EntradasRecebidas.AddRange(entradas);
            db.Interacoes.AddRange(interacoes);
            db.HistoricoEtapas.AddRange(historico);
        }

        await DevolverAVezAsync(agora, ct);
        var totalEntradas = massa.Sum(m => m.Entradas.Count);
        auditoria.Registrar(AcoesAuditoria.DemonstracaoPreparada, detalhes: new { leads = massa.Count, entradas = totalEntradas, leadsApagados });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        var ganhos = massa.Count(m => m.Lead.ConvertidoEm is not null);
        var perdidos = massa.Count(m => m.Lead.PerdidoEm is not null);
        return new ResultadoDemonstracao(
            true,
            $"Demonstração preparada: {massa.Count} leads ({ganhos} ganhos e {perdidos} perdidos) e {totalEntradas} entradas, "
            + $"divididos entre {atendentes.Count} atendente(s). Saíram {leadsApagados} leads antigos. Recarregue as telas abertas.");
    }

    /// <summary>
    /// Apaga os leads e o que depende deles, as importações e as campanhas, na ordem das chaves estrangeiras. Devolve
    /// quantos leads saíram.
    /// </summary>
    private async Task<int> ApagarAsync(CancellationToken ct)
    {
        await db.Notificacoes.ExecuteDeleteAsync(ct);
        await db.EventosSaida.ExecuteDeleteAsync(ct);
        await db.Interacoes.ExecuteDeleteAsync(ct);
        await db.HistoricoEtapas.ExecuteDeleteAsync(ct);
        await db.EntradasRecebidas.ExecuteDeleteAsync(ct);
        await db.ErrosImportacao.ExecuteDeleteAsync(ct);
        await db.Importacoes.ExecuteDeleteAsync(ct);
        var leads = await db.Leads.ExecuteDeleteAsync(ct);
        await db.Campanhas.ExecuteDeleteAsync(ct);
        return leads;
    }

    /// <summary>
    /// A vez do rodízio volta para quem vem primeiro em ordem alfabética entre os que participam: o primeiro lead da
    /// demonstração vai sempre para a mesma pessoa. Quem participa não muda.
    /// </summary>
    private async Task DevolverAVezAsync(DateTimeOffset agora, CancellationToken ct)
    {
        var participantes = EmOrdemAlfabetica(
            await db.Users.Where(u => u.Ativo && u.ParticipaRodizio).Select(u => new { u.Id, u.Nome }).ToListAsync(ct),
            u => u.Nome,
            u => u.Id);
        for (var posicao = 0; posicao < participantes.Count; posicao++)
        {
            // Sem lead pelo rodízio vem primeiro; depois, quem recebeu há mais tempo.
            var id = participantes[posicao];
            DateTimeOffset? vez = posicao == 0 ? null : agora.AddMinutes(-(participantes.Count - posicao));
            await db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.UltimoLeadAtribuidoEm, vez), ct);
        }
    }

    private static List<Guid> EmOrdemAlfabetica<T>(IEnumerable<T> usuarios, Func<T, string> nome, Func<T, Guid> id) =>
        [.. usuarios.OrderBy(nome, MetricasPainel.OrdemDosNomes).ThenBy(id).Select(id)];
}
