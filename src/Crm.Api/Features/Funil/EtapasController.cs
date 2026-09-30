using System.Globalization;
using System.Security.Claims;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Funil;

/// <summary>
/// Etapas do funil (seção 3.4). A leitura é liberada para a atendente, que usa as etapas nos filtros e no
/// quadro; o resto é do admin. Etapa nova é sempre aberta; Ganho e Perdido existem sempre, uma de cada, e só
/// podem ser renomeadas.
/// </summary>
[ApiController]
[Route("api/etapas")]
[Authorize]
public sealed class EtapasController(
    CrmDbContext db,
    MudancaEtapa mudancaEtapa,
    AuditoriaService auditoria,
    TimeProvider relogio) : ControllerBase
{
    /// <summary>Todas as etapas, ativas ou não, na ordem do funil.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EtapaResponse>>> Listar() =>
        await db.Etapas
            .OrderBy(e => e.Ordem)
            .ThenBy(e => e.Id)
            .Select(e => new EtapaResponse(e.Id, e.Nome, e.Ordem, e.Tipo, e.Ativa))
            .ToListAsync(HttpContext.RequestAborted);

    /// <summary>Cria uma etapa aberta, no fim das abertas (antes de Ganho e Perdido).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType<EtapaResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EtapaResponse>> Criar(CriarEtapaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var nome = Textos.Limpar(requisicao.Nome)!;
        if (await NomeEmUsoAsync(nome, id: null, ct))
        {
            return NomeRepetido();
        }

        var etapas = await db.Etapas.ToListAsync(ct);
        var etapa = new Etapa { Nome = nome, Tipo = TipoEtapa.Aberta };
        db.Etapas.Add(etapa);
        RegrasEtapas.Numerar([.. Abertas(etapas), etapa], Fechamento(etapas));
        await db.SaveChangesAsync(ct);

        auditoria.Registrar(AcoesAuditoria.EtapaCriada, EntidadesAuditoria.Etapa, Id(etapa), new { nome });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ParaResposta(etapa));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<EtapaResponse>> Renomear(int id, RenomearEtapaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var etapa = await db.Etapas.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (etapa is null)
        {
            return NotFound();
        }

        var nome = Textos.Limpar(requisicao.Nome)!;
        if (nome == etapa.Nome)
        {
            return ParaResposta(etapa);
        }

        if (await NomeEmUsoAsync(nome, id, ct))
        {
            return NomeRepetido();
        }

        etapa.Nome = nome;
        auditoria.Registrar(AcoesAuditoria.EtapaEditada, EntidadesAuditoria.Etapa, Id(etapa), new { campos = new[] { "nome" } });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return ParaResposta(etapa);
    }

    /// <summary>Muda a ordem das etapas abertas. Ganho e Perdido ficam sempre no fim.</summary>
    [HttpPut("ordem")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<IReadOnlyList<EtapaResponse>>> Reordenar(ReordenarEtapasRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var etapas = await db.Etapas.ToListAsync(ct);
        var abertas = etapas.Where(e => e.Tipo == TipoEtapa.Aberta).ToDictionary(e => e.Id);
        if (requisicao.Ids.Count != abertas.Count || !requisicao.Ids.All(abertas.ContainsKey))
        {
            ModelState.AddModelError("ids", RegrasEtapas.MensagemOrdemIncompleta);
            return ValidationProblem(ModelState);
        }

        RegrasEtapas.Numerar([.. requisicao.Ids.Select(id => abertas[id])], Fechamento(etapas));
        auditoria.Registrar(AcoesAuditoria.EtapasReordenadas, EntidadesAuditoria.Etapa, detalhes: new { ids = requisicao.Ids });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return await Listar();
    }

    /// <summary>
    /// Desativa uma etapa aberta. Se ela tiver leads, eles vão para a etapa escolhida na mesma transação, com uma
    /// linha no histórico cada. Sem destino, responde 409 com a quantidade de leads.
    /// </summary>
    [HttpPost("{id:int}/desativar")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<EtapaResponse>> Desativar(int id, DesativarEtapaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);

        // FOR UPDATE espera as entradas e mudanças que já travaram a etapa com FOR SHARE; depois disso, nenhum
        // lead novo entra nela.
        var etapa = (await db.Etapas.FromSql($"SELECT * FROM etapas WHERE id = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (etapa is null)
        {
            return NotFound();
        }

        var abertasAtivas = await db.Etapas.CountAsync(e => e.Ativa && e.Tipo == TipoEtapa.Aberta, ct);
        if (RegrasEtapas.ConferirDesativacao(etapa, abertasAtivas) is { } erro)
        {
            return Problem(detail: erro, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!etapa.Ativa)
        {
            return ParaResposta(etapa);
        }

        // Trava os leads da etapa antes de lê-los: uma mudança de etapa ou uma entrada simultânea espera.
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM leads WHERE etapa_id = {id} FOR UPDATE", ct);
        var leads = await db.Leads.Where(l => l.EtapaId == id).ToListAsync(ct);
        Etapa? destino = null;
        if (leads.Count > 0)
        {
            if (requisicao.MoverLeadsParaId is not { } destinoId)
            {
                var problema = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext, StatusCodes.Status409Conflict, detail: RegrasEtapas.MensagemTemLeads(leads.Count));
                problema.Extensions["quantidadeLeads"] = leads.Count;
                return Conflict(problema);
            }

            destino = await db.Etapas.AtivaParaMoverAsync(destinoId, ct);
            if (destino is not { Tipo: TipoEtapa.Aberta } || destino.Id == id)
            {
                ModelState.AddModelError("moverLeadsParaId", RegrasEtapas.MensagemDestinoInvalido);
                return ValidationProblem(ModelState);
            }

            // Mudança de organização, e não contato com o lead: não grava o primeiro contato.
            var agora = relogio.GetUtcNow();
            var usuarioId = UsuarioLogado();
            foreach (var lead in leads)
            {
                await mudancaEtapa.RegistrarAsync(lead, destino, etapaInicialId: etapa.Id, null, null, usuarioId, agora, contaComoContato: false, ct);
            }
        }

        etapa.Ativa = false;
        auditoria.Registrar(
            AcoesAuditoria.EtapaDesativada, EntidadesAuditoria.Etapa, Id(etapa), new { leadsMovidos = leads.Count, destinoId = destino?.Id });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return ParaResposta(etapa);
    }

    [HttpPost("{id:int}/reativar")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<EtapaResponse>> Reativar(int id)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var etapa = await db.Etapas.SingleOrDefaultAsync(e => e.Id == id, ct);
        if (etapa is null)
        {
            return NotFound();
        }

        if (etapa.Tipo != TipoEtapa.Aberta)
        {
            return Problem(detail: RegrasEtapas.MensagemFechamentoFixo, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!etapa.Ativa)
        {
            etapa.Ativa = true;
            auditoria.Registrar(AcoesAuditoria.EtapaReativada, EntidadesAuditoria.Etapa, Id(etapa));
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }

        return ParaResposta(etapa);
    }

    /// <summary>Uma alteração de etapas por vez: a ordem e os nomes são conferidos sem corrida.</summary>
    private Task TravarCadastroAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended('cadastro:etapas', 0))", ct);

    private Task<bool> NomeEmUsoAsync(string nome, int? id, CancellationToken ct) =>
        db.Etapas.AnyAsync(e => e.Id != id && e.Nome.ToLower() == nome.ToLower(), ct);

    private ActionResult NomeRepetido()
    {
        ModelState.AddModelError("nome", RegrasEtapas.MensagemNomeRepetido);
        return ValidationProblem(ModelState);
    }

    private Guid? UsuarioLogado() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static List<Etapa> Abertas(IEnumerable<Etapa> etapas) =>
        [.. etapas.Where(e => e.Tipo == TipoEtapa.Aberta).OrderBy(e => e.Ordem).ThenBy(e => e.Id)];

    private static IEnumerable<Etapa> Fechamento(IEnumerable<Etapa> etapas) => etapas.Where(e => e.Tipo != TipoEtapa.Aberta);

    private static string Id(Etapa etapa) => etapa.Id.ToString(CultureInfo.InvariantCulture);

    private static EtapaResponse ParaResposta(Etapa etapa) => new(etapa.Id, etapa.Nome, etapa.Ordem, etapa.Tipo, etapa.Ativa);
}
