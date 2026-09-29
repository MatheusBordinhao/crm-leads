using System.Globalization;
using System.Text.Json;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Interações e linha do tempo da ficha (seção 3.5). Como o resto da API de leads, parte de
/// <see cref="VisibilidadeLeads"/>: o lead de outra pessoa responde 404. Interações não são editadas nem
/// excluídas, por isso só há o registro.
/// </summary>
[ApiController]
[Route("api/leads/{id:long}")]
[Authorize]
public sealed class LinhaDoTempoController(CrmDbContext db, VisibilidadeLeads visibilidade, TimeProvider relogio) : ControllerBase
{
    /// <summary>Entradas, interações, mudanças de etapa e trocas de responsável, da mais recente para a mais antiga.</summary>
    [HttpGet("linha-do-tempo")]
    public async Task<ActionResult<IReadOnlyList<ItemLinhaDoTempo>>> Listar(long id)
    {
        var ct = HttpContext.RequestAborted;
        if (!await visibilidade.Visiveis().AnyAsync(l => l.Id == id, ct))
        {
            return NotFound();
        }

        var itens = new List<(ItemLinhaDoTempo Item, long Sequencia)>();
        itens.AddRange(await EntradasAsync(id, ct));
        itens.AddRange(await InteracoesAsync(id, ct));
        itens.AddRange(await MudancasDeEtapaAsync(id, ct));
        itens.AddRange(await TrocasDeResponsavelAsync(id, ct));

        // No mesmo instante, a consequência vem antes da causa: a reabertura aparece acima da entrada que a causou.
        return itens
            .OrderByDescending(i => i.Item.OcorridoEm)
            .ThenByDescending(i => i.Item.Tipo)
            .ThenByDescending(i => i.Sequencia)
            .Select(i => i.Item)
            .ToList();
    }

    /// <summary>
    /// Registra uma interação. A primeira ligação, WhatsApp, e-mail ou reunião grava o primeiro contato do lead,
    /// uma vez só. Uma data até 5 minutos no futuro (relógio do navegador adiantado) vale como agora.
    /// </summary>
    [HttpPost("interacoes")]
    [ProducesResponseType<ItemLinhaDoTempo>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ItemLinhaDoTempo>> Registrar(long id, RegistrarInteracaoRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Travado antes de conferir se é visível, como na mudança de etapa: duas interações simultâneas não
        // gravam o primeiro contato duas vezes.
        await db.TravarLeadAsync(id, ct);
        var lead = await visibilidade.Visiveis().SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null || visibilidade.UsuarioId is not { } usuarioId)
        {
            return NotFound();
        }

        var agora = relogio.GetUtcNow();
        var ocorridoEm = requisicao.OcorridoEm > agora ? agora : requisicao.OcorridoEm.ToUniversalTime();
        var descricao = requisicao.Descricao?.Trim();
        var interacao = new Interacao
        {
            LeadId = id,
            UsuarioId = usuarioId,
            Tipo = requisicao.Tipo,
            Descricao = string.IsNullOrEmpty(descricao) ? null : descricao,
            OcorridoEm = ocorridoEm,
            CriadoEm = agora,
        };
        db.Interacoes.Add(interacao);
        if (Interacao.EhContato(interacao.Tipo))
        {
            lead.RegistrarPrimeiroContato(ocorridoEm);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);

        var usuario = await db.Users.Where(u => u.Id == usuarioId).Select(u => new UsuarioResumo(u.Id, u.Nome)).SingleAsync(ct);
        var item = new ItemLinhaDoTempo(
            TipoItemLinhaDoTempo.Interacao,
            interacao.OcorridoEm,
            usuario,
            null,
            new InteracaoNaLinhaDoTempo(interacao.Id, interacao.Tipo, interacao.Descricao, interacao.CriadoEm),
            null,
            null);
        return StatusCode(StatusCodes.Status201Created, item);
    }

    private async Task<IEnumerable<(ItemLinhaDoTempo, long)>> EntradasAsync(long id, CancellationToken ct)
    {
        var entradas = await db.EntradasRecebidas
            .Where(e => e.LeadId == id)
            .Select(e => new
            {
                e.Id,
                e.ProcessadoEm,
                Entrada = new EntradaNaLinhaDoTempo(
                    e.Origem,
                    db.Campanhas.Where(c => c.Id == e.CampanhaId).Select(c => new CampanhaResumo(c.Id, c.Nome)).FirstOrDefault(),
                    e.Situacao,
                    e.Teste),
            })
            .ToListAsync(ct);
        return entradas.Select(e => (
            new ItemLinhaDoTempo(TipoItemLinhaDoTempo.Entrada, e.ProcessadoEm, null, e.Entrada, null, null, null),
            e.Id));
    }

    private async Task<IEnumerable<(ItemLinhaDoTempo, long)>> InteracoesAsync(long id, CancellationToken ct)
    {
        var interacoes = await db.Interacoes
            .Where(i => i.LeadId == id)
            .Select(i => new
            {
                i.Id,
                i.OcorridoEm,
                Usuario = db.Users.Where(u => u.Id == i.UsuarioId).Select(u => new UsuarioResumo(u.Id, u.Nome)).First(),
                Interacao = new InteracaoNaLinhaDoTempo(i.Id, i.Tipo, i.Descricao, i.CriadoEm),
            })
            .ToListAsync(ct);
        return interacoes.Select(i => (
            new ItemLinhaDoTempo(TipoItemLinhaDoTempo.Interacao, i.OcorridoEm, i.Usuario, null, i.Interacao, null, null),
            i.Id));
    }

    private async Task<IEnumerable<(ItemLinhaDoTempo, long)>> MudancasDeEtapaAsync(long id, CancellationToken ct)
    {
        var mudancas = await db.HistoricoEtapas
            .Where(h => h.LeadId == id)
            .Select(h => new
            {
                h.Id,
                h.OcorridoEm,
                Usuario = db.Users.Where(u => u.Id == h.UsuarioId).Select(u => new UsuarioResumo(u.Id, u.Nome)).FirstOrDefault(),
                Etapa = new MudancaEtapaNaLinhaDoTempo(
                    db.Etapas.Where(e => e.Id == h.EtapaAnteriorId).Select(e => new EtapaResumo(e.Id, e.Nome, e.Tipo)).First(),
                    db.Etapas.Where(e => e.Id == h.EtapaNovaId).Select(e => new EtapaResumo(e.Id, e.Nome, e.Tipo)).First(),
                    db.MotivosPerda.Where(m => m.Id == h.MotivoPerdaId).Select(m => new MotivoPerdaResumo(m.Id, m.Nome)).FirstOrDefault(),
                    h.MotivoPerdaDetalhe),
            })
            .ToListAsync(ct);
        return mudancas.Select(m => (
            new ItemLinhaDoTempo(TipoItemLinhaDoTempo.Etapa, m.OcorridoEm, m.Usuario, null, null, m.Etapa, null),
            m.Id));
    }

    /// <summary>As trocas de responsável ficam na auditoria (<see cref="AcoesAuditoria.LeadReatribuido"/>).</summary>
    private async Task<IEnumerable<(ItemLinhaDoTempo, long)>> TrocasDeResponsavelAsync(long id, CancellationToken ct)
    {
        var entidadeId = id.ToString(CultureInfo.InvariantCulture);
        var trocas = await db.Auditoria
            .AsNoTracking()
            .Where(r => r.Acao == AcoesAuditoria.LeadReatribuido && r.Entidade == EntidadesAuditoria.Lead && r.EntidadeId == entidadeId)
            .ToListAsync(ct);
        if (trocas.Count == 0)
        {
            return [];
        }

        var detalhes = trocas.Select(t => (Registro: t, Anterior: LerId(t.Detalhes, "anteriorId"), Novo: LerId(t.Detalhes, "novoId"))).ToList();
        var ids = detalhes.SelectMany(d => new[] { d.Anterior, d.Novo, d.Registro.UsuarioId }).OfType<Guid>().Distinct().ToList();
        var usuarios = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new UsuarioResumo(u.Id, u.Nome), ct);
        UsuarioResumo? Usuario(Guid? usuarioId) => usuarioId is { } chave ? usuarios.GetValueOrDefault(chave) : null;

        return detalhes.Select(d => (
            new ItemLinhaDoTempo(
                TipoItemLinhaDoTempo.Responsavel,
                d.Registro.OcorridoEm,
                Usuario(d.Registro.UsuarioId),
                null,
                null,
                null,
                new TrocaResponsavelNaLinhaDoTempo(Usuario(d.Anterior), Usuario(d.Novo))),
            d.Registro.Id));
    }

    private static Guid? LerId(JsonDocument? detalhes, string propriedade) =>
        detalhes?.RootElement.ValueKind == JsonValueKind.Object
        && detalhes.RootElement.TryGetProperty(propriedade, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && Guid.TryParse(valor.GetString(), out var id)
            ? id
            : null;
}
