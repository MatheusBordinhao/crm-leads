using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>LGPD (seção 3.8), só para o admin: a atendente recebe 403. Tudo entra na auditoria.</summary>
[ApiController]
[Route("api/leads/{id:long}")]
[Authorize(Policy = Politicas.Admin)]
public sealed class LgpdController(
    CrmDbContext db,
    VisibilidadeLeads visibilidade,
    Anonimizacao anonimizacao,
    AuditoriaService auditoria,
    TimeProvider relogio) : ControllerBase
{
    /// <summary>
    /// O arquivo para o titular, indentado e com os acentos como são, para ser lido por uma pessoa: o codificador padrão
    /// escreveria <c>Conceição</c>. Sem o escape de HTML, que serve para JSON dentro de uma página: o arquivo é
    /// baixado como <c>application/json</c>.
    /// </summary>
    private static readonly JsonSerializerOptions JsonDoArquivo = new(JsonSerializerOptions.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Todos os dados que o CRM guarda do lead, para entregar ao titular, num arquivo <c>dados-titular-lead-{id}.json</c>:
    /// o lead, as entradas com o payload bruto, as interações, o histórico de etapas e os envios ao n8n. A exportação entra
    /// na auditoria.
    /// </summary>
    [HttpGet("dados-titular")]
    [ProducesResponseType<DadosTitularResponse>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> DadosDoTitular(long id)
    {
        var ct = HttpContext.RequestAborted;
        var lead = await visibilidade.Visiveis()
            .AsNoTracking()
            .Include(l => l.Etapa)
            .Include(l => l.Responsavel)
            .Include(l => l.Campanha)
            .Include(l => l.MotivoPerda)
            .SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null)
        {
            return NotFound();
        }

        var dados = new DadosTitularResponse(
            relogio.GetUtcNow(),
            DoLead(lead),
            await EntradasAsync(id, ct),
            await InteracoesAsync(id, ct),
            await HistoricoAsync(id, ct),
            await EnviosAsync(id, ct));
        auditoria.Registrar(AcoesAuditoria.LeadDadosExportados, EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(ct);

        return File(
            JsonSerializer.SerializeToUtf8Bytes(dados, JsonDoArquivo),
            "application/json",
            $"dados-titular-lead-{id.ToString(CultureInfo.InvariantCulture)}.json");
    }

    /// <summary>
    /// Anonimiza o lead, sem volta: tira os dados pessoais do lead, das entradas, das interações e do histórico, e apaga os
    /// eventos para o n8n. Etapa, datas, origem, campanha e responsável continuam, para o painel manter os totais.
    /// Anonimizar de novo não muda nada. Com uma importação processando, responde 409: o arquivo dela ainda guarda os dados
    /// das pessoas.
    /// </summary>
    [HttpPost("anonimizar")]
    public async Task<ActionResult<LeadResponse>> Anonimizar(long id)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Travado antes da leitura, como na mudança de etapa: uma entrada da mesma pessoa espera e depois não acha mais o lead.
        await db.TravarLeadAsync(id, ct);
        var lead = await visibilidade.Visiveis().SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null)
        {
            return NotFound();
        }

        if (!await anonimizacao.AnonimizarAsync(lead, ct))
        {
            return Problem(detail: Anonimizacao.MensagemImportacaoEmAndamento, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return (await FichaLead.BuscarAsync(visibilidade.Visiveis(), id, ct))!;
    }

    private static DadosTitularLead DoLead(Lead lead) => new(
        lead.Id,
        lead.Nome,
        lead.Email,
        lead.Telefone,
        lead.CpfCnpj,
        lead.Uf,
        lead.Empresa,
        lead.Origem,
        lead.Plataforma,
        lead.Campanha?.Nome,
        lead.UtmSource,
        lead.UtmMedium,
        lead.UtmCampaign,
        lead.UtmContent,
        lead.UtmTerm,
        lead.Gclid,
        lead.RespostasExtras?.RootElement.Clone(),
        lead.Etapa.Nome,
        lead.Responsavel?.Nome,
        lead.MotivoPerda?.Nome,
        lead.MotivoPerdaDetalhe,
        lead.Teste,
        lead.CriadoEm,
        lead.AtualizadoEm,
        lead.PrimeiroContatoEm,
        lead.ConvertidoEm,
        lead.PerdidoEm,
        lead.AnonimizadoEm);

    private async Task<List<DadosTitularEntrada>> EntradasAsync(long id, CancellationToken ct)
    {
        var entradas = await db.EntradasRecebidas
            .AsNoTracking()
            .Where(e => e.LeadId == id)
            .OrderBy(e => e.Id)
            .Select(e => new { Entrada = e, Campanha = db.Campanhas.Where(c => c.Id == e.CampanhaId).Select(c => c.Nome).FirstOrDefault() })
            .ToListAsync(ct);
        return [.. entradas.Select(e => new DadosTitularEntrada(
            e.Entrada.Origem,
            e.Entrada.IdExterno,
            e.Campanha,
            e.Entrada.Conjunto?.RootElement.Clone(),
            e.Entrada.Anuncio?.RootElement.Clone(),
            e.Entrada.Formulario?.RootElement.Clone(),
            e.Entrada.UtmSource,
            e.Entrada.UtmMedium,
            e.Entrada.UtmCampaign,
            e.Entrada.UtmContent,
            e.Entrada.UtmTerm,
            e.Entrada.Gclid,
            e.Entrada.RespostasExtras?.RootElement.Clone(),
            e.Entrada.PayloadBruto?.RootElement.Clone(),
            e.Entrada.Situacao,
            e.Entrada.Teste,
            e.Entrada.RecebidoEm,
            e.Entrada.ProcessadoEm))];
    }

    private Task<List<DadosTitularInteracao>> InteracoesAsync(long id, CancellationToken ct) =>
        db.Interacoes
            .Where(i => i.LeadId == id)
            .OrderBy(i => i.OcorridoEm)
            .ThenBy(i => i.Id)
            .Select(i => new DadosTitularInteracao(
                i.Tipo, i.Descricao, i.OcorridoEm, db.Users.Where(u => u.Id == i.UsuarioId).Select(u => u.Nome).First(), i.CriadoEm))
            .ToListAsync(ct);

    private Task<List<DadosTitularMudancaEtapa>> HistoricoAsync(long id, CancellationToken ct) =>
        db.HistoricoEtapas
            .Where(h => h.LeadId == id)
            .OrderBy(h => h.OcorridoEm)
            .ThenBy(h => h.Id)
            .Select(h => new DadosTitularMudancaEtapa(
                db.Etapas.Where(e => e.Id == h.EtapaAnteriorId).Select(e => e.Nome).First(),
                db.Etapas.Where(e => e.Id == h.EtapaNovaId).Select(e => e.Nome).First(),
                db.MotivosPerda.Where(m => m.Id == h.MotivoPerdaId).Select(m => m.Nome).FirstOrDefault(),
                h.MotivoPerdaDetalhe,
                db.Users.Where(u => u.Id == h.UsuarioId).Select(u => u.Nome).FirstOrDefault(),
                h.OcorridoEm))
            .ToListAsync(ct);

    private Task<List<DadosTitularEnvio>> EnviosAsync(long id, CancellationToken ct) =>
        db.EventosSaida
            .Where(e => e.LeadId == id)
            .OrderBy(e => e.CriadoEm)
            .ThenBy(e => e.Id)
            .Select(e => new DadosTitularEnvio(e.Tipo, e.Situacao, e.CriadoEm, e.EntregueEm))
            .ToListAsync(ct);
}
