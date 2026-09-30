using System.Globalization;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Funil;

public sealed record MotivoPerdaResponse(int Id, string Nome, bool Ativo);

public sealed record CriarMotivoPerdaRequest(string Nome) : ICadastroComNome
{
    public override string ToString() => nameof(CriarMotivoPerdaRequest);
}

/// <param name="Ativo">Falso desativa: o motivo sai da lista de escolha, mas continua nos leads e no histórico.</param>
public sealed record EditarMotivoPerdaRequest(string Nome, bool Ativo) : ICadastroComNome
{
    public override string ToString() => $"{nameof(EditarMotivoPerdaRequest)} {{ Ativo = {Ativo} }}";
}

internal sealed class CriarMotivoPerdaRequestValidator : Validador<CriarMotivoPerdaRequest>
{
    public CriarMotivoPerdaRequestValidator() => RegrasNome.Aplicar(this, MotivoPerda.TamanhoMaximoNome);
}

internal sealed class EditarMotivoPerdaRequestValidator : Validador<EditarMotivoPerdaRequest>
{
    public EditarMotivoPerdaRequestValidator() => RegrasNome.Aplicar(this, MotivoPerda.TamanhoMaximoNome);
}

/// <summary>
/// Motivos de perda (seção 3.4). A leitura é liberada para a atendente, que escolhe o motivo ao levar um lead
/// para Perdido; o cadastro é do admin. Motivo não é excluído: é desativado.
/// </summary>
[ApiController]
[Route("api/motivos-perda")]
[Authorize]
public sealed class MotivosPerdaController(CrmDbContext db, AuditoriaService auditoria) : ControllerBase
{
    public const string MensagemNomeRepetido = "Já existe um motivo com esse nome.";

    public const string MensagemUltimoAtivo = "Mantenha pelo menos um motivo de perda ativo.";

    /// <summary>Todos os motivos, ativos ou não, na ordem em que foram cadastrados.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MotivoPerdaResponse>>> Listar() =>
        await db.MotivosPerda
            .OrderBy(m => m.Id)
            .Select(m => new MotivoPerdaResponse(m.Id, m.Nome, m.Ativo))
            .ToListAsync(HttpContext.RequestAborted);

    [HttpPost]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType<MotivoPerdaResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MotivoPerdaResponse>> Criar(CriarMotivoPerdaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var nome = Textos.Limpar(requisicao.Nome)!;
        if (await NomeEmUsoAsync(nome, id: null, ct))
        {
            return NomeRepetido();
        }

        var motivo = new MotivoPerda { Nome = nome };
        db.MotivosPerda.Add(motivo);
        await db.SaveChangesAsync(ct);
        auditoria.Registrar(AcoesAuditoria.MotivoPerdaCriado, EntidadesAuditoria.MotivoPerda, Id(motivo), new { nome });
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ParaResposta(motivo));
    }

    /// <summary>Renomeia, desativa ou reativa. O último motivo ativo não pode ser desativado.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Politicas.Admin)]
    public async Task<ActionResult<MotivoPerdaResponse>> Editar(int id, EditarMotivoPerdaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await TravarCadastroAsync(ct);
        var motivo = await db.MotivosPerda.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (motivo is null)
        {
            return NotFound();
        }

        var nome = Textos.Limpar(requisicao.Nome)!;
        var campos = new List<string>();
        if (nome != motivo.Nome)
        {
            if (await NomeEmUsoAsync(nome, id, ct))
            {
                return NomeRepetido();
            }

            campos.Add("nome");
        }

        if (requisicao.Ativo != motivo.Ativo)
        {
            if (!requisicao.Ativo && await db.MotivosPerda.CountAsync(m => m.Ativo, ct) <= 1)
            {
                return Problem(detail: MensagemUltimoAtivo, statusCode: StatusCodes.Status400BadRequest);
            }

            campos.Add("ativo");
        }

        if (campos.Count > 0)
        {
            motivo.Nome = nome;
            motivo.Ativo = requisicao.Ativo;
            auditoria.Registrar(AcoesAuditoria.MotivoPerdaEditado, EntidadesAuditoria.MotivoPerda, Id(motivo), new { campos });
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }

        return ParaResposta(motivo);
    }

    /// <summary>Uma alteração de motivos por vez: o nome repetido e o último ativo são conferidos sem corrida.</summary>
    private Task TravarCadastroAsync(CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended('cadastro:motivos_perda', 0))", ct);

    private Task<bool> NomeEmUsoAsync(string nome, int? id, CancellationToken ct) =>
        db.MotivosPerda.AnyAsync(m => m.Id != id && m.Nome.ToLower() == nome.ToLower(), ct);

    private ActionResult NomeRepetido()
    {
        ModelState.AddModelError("nome", MensagemNomeRepetido);
        return ValidationProblem(ModelState);
    }

    private static string Id(MotivoPerda motivo) => motivo.Id.ToString(CultureInfo.InvariantCulture);

    private static MotivoPerdaResponse ParaResposta(MotivoPerda motivo) => new(motivo.Id, motivo.Nome, motivo.Ativo);
}
