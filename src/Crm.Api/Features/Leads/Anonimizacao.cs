using System.Globalization;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Importacoes;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Anonimização do lead (seção 3.8), que não tem volta. Tira os dados pessoais do lead, das entradas, das interações e do
/// histórico de etapas, troca o id externo das entradas pelo hash (<see cref="IdExternoAnonimo"/>) e apaga os eventos para
/// o n8n, cujo payload leva o nome e a empresa. Mantém a etapa, as datas, a origem, a campanha e a responsável: o painel e
/// o relatório continuam com os mesmos números. As notificações ficam como estão, porque o título nunca leva dados do lead.
/// </summary>
public sealed class Anonimizacao(CrmDbContext db, AuditoriaService auditoria, TimeProvider relogio)
{
    public const string NomeDoTitularRemovido = "Titular removido";

    public const string MensagemDadosRemovidos = "Os dados pessoais deste lead foram removidos e não podem voltar.";

    public const string MensagemSemInteracoes = "Este lead foi anonimizado e não recebe novas interações.";

    public const string MensagemSemDetalhePerda = "Este lead foi anonimizado e não recebe o detalhe do motivo da perda.";

    public const string MensagemImportacaoEmAndamento =
        "Há uma importação em andamento. Tente de novo quando ela terminar: o arquivo dela ainda guarda os dados das pessoas.";

    /// <summary>
    /// Anonimiza o lead, que já precisa estar travado (<see cref="TravasLeads.TravarLeadAsync"/>) e carregado pelo
    /// contexto. O lead, as entradas e a auditoria são gravados no próximo <c>SaveChanges</c>; as interações, o histórico e
    /// os eventos mudam na hora, na transação aberta. Um lead já anonimizado não muda.
    /// </summary>
    /// <returns>
    /// Falso, sem mudar nada, quando há uma importação processando: o arquivo dela, que só é apagado no fim, pode ter os
    /// dados da pessoa.
    /// </returns>
    public async Task<bool> AnonimizarAsync(Lead lead, CancellationToken ct)
    {
        if (lead.AnonimizadoEm is not null)
        {
            return true;
        }

        if (await db.Importacoes.AnyAsync(i => i.Situacao == SituacaoImportacao.Processando, ct))
        {
            return false;
        }

        var agora = relogio.GetUtcNow();
        lead.Nome = NomeDoTitularRemovido;
        lead.Email = null;
        lead.Telefone = null;
        lead.CpfCnpj = null;
        lead.Empresa = null;
        lead.RespostasExtras = null;
        lead.MotivoPerdaDetalhe = null;
        lead.Gclid = null;
        lead.AnonimizadoEm = agora;
        lead.AtualizadoEm = agora;

        // O hash do id externo é calculado aqui, e não no banco, para ser o mesmo que a entrada procura.
        var entradas = await db.EntradasRecebidas.Where(e => e.LeadId == lead.Id).ToListAsync(ct);
        foreach (var entrada in entradas)
        {
            entrada.IdExterno = entrada.IdExterno is { } idExterno ? IdExternoAnonimo.De(idExterno) : null;
            entrada.PayloadBruto = null;
            entrada.RespostasExtras = null;
            entrada.Gclid = null;
        }

        var interacoes = await db.Interacoes
            .Where(i => i.LeadId == lead.Id)
            .ExecuteUpdateAsync(i => i.SetProperty(x => x.Descricao, (string?)null), ct);
        await db.HistoricoEtapas
            .Where(h => h.LeadId == lead.Id && h.MotivoPerdaDetalhe != null)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.MotivoPerdaDetalhe, (string?)null), ct);
        var eventos = await db.EventosSaida.Where(e => e.LeadId == lead.Id).ExecuteDeleteAsync(ct);

        auditoria.Registrar(
            AcoesAuditoria.LeadAnonimizado,
            EntidadesAuditoria.Lead,
            lead.Id.ToString(CultureInfo.InvariantCulture),
            new { entradas = entradas.Count, interacoes, eventos });
        return true;
    }
}
