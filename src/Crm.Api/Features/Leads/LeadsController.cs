using System.Globalization;
using System.Text.Json;
using Crm.Api.Features.Funil;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Datas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>
/// Leads para a tela. Toda consulta passa por <see cref="VisibilidadeLeads"/>: a atendente só vê os próprios
/// leads, e o lead de outra pessoa responde 404.
/// </summary>
[ApiController]
[Route("api/leads")]
[Authorize]
public sealed class LeadsController(
    CrmDbContext db,
    VisibilidadeLeads visibilidade,
    ReceberEntrada receberEntrada,
    MudancaEtapa mudancaEtapa,
    AuditoriaService auditoria,
    TimeProvider relogio) : ControllerBase
{
    public const string MensagemConflito = "O lead foi alterado por outra pessoa. Recarregue a página para ver a versão atual.";

    /// <summary>Lista com busca, filtros, ordenação e paginação.</summary>
    [HttpGet]
    public async Task<ActionResult<ListaLeadsResponse>> Listar([FromQuery] FiltroLeads filtro)
    {
        var ct = HttpContext.RequestAborted;
        var consulta = Filtrar(visibilidade.Visiveis(), filtro);
        var total = await consulta.CountAsync(ct);

        IOrderedQueryable<Lead> ordenada = filtro.Ordem switch
        {
            OrdemLeads.Antigos => consulta.OrderBy(l => l.CriadoEm).ThenBy(l => l.Id),
            OrdemLeads.Nome => consulta.OrderBy(l => l.Nome).ThenBy(l => l.Id),
            _ => consulta.OrderByDescending(l => l.CriadoEm).ThenByDescending(l => l.Id),
        };
        var itens = await ordenada
            .Skip((filtro.Pagina - 1) * filtro.Tamanho)
            .Take(filtro.Tamanho)
            .Select(l => new LeadResumoResponse(
                l.Id,
                l.Nome,
                l.Empresa,
                l.Email,
                l.Telefone,
                new EtapaResumo(l.Etapa.Id, l.Etapa.Nome, l.Etapa.Tipo),
                l.Origem,
                l.Campanha == null ? null : new CampanhaResumo(l.Campanha.Id, l.Campanha.Nome),
                l.Responsavel == null ? null : new ResponsavelResumo(l.Responsavel.Id, l.Responsavel.Nome),
                l.Teste,
                l.CriadoEm))
            .ToListAsync(ct);

        return new ListaLeadsResponse(itens, total, filtro.Pagina, filtro.Tamanho);
    }

    /// <summary>Ficha do lead, com os avisos sobre os dados.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<LeadResponse>> Obter(long id)
    {
        var lead = await BuscarFichaAsync(id);
        return lead is null ? NotFound() : lead;
    }

    /// <summary>
    /// Cadastro manual, pelo mesmo caminho das outras entradas: se a pessoa já é um lead, os dados vão para a
    /// ficha dele (<c>vinculado</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CriarLeadResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CriarLeadResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CriarLeadResponse>> Criar(CriarLeadRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        var usuarioId = visibilidade.UsuarioId;
        Guid? responsavelId;
        if (visibilidade.EhAdmin)
        {
            if (requisicao.ResponsavelId is { } escolhido && !await db.Users.AnyAsync(u => u.Id == escolhido && u.Ativo, ct))
            {
                ModelState.AddModelError("responsavelId", "Responsável não encontrado ou desativado.");
                return ValidationProblem(ModelState);
            }

            responsavelId = requisicao.ResponsavelId;
        }
        else
        {
            if (requisicao.ResponsavelId is { } outro && outro != usuarioId)
            {
                ModelState.AddModelError("responsavelId", "Só um admin escolhe a responsável.");
                return ValidationProblem(ModelState);
            }

            responsavelId = usuarioId;
        }

        var processada = await receberEntrada.ExecutarAsync(
            new NovaEntrada
            {
                Origem = requisicao.Origem,
                RecebidoEm = relogio.GetUtcNow(),
                Nome = requisicao.Nome,
                Email = requisicao.Email,
                Telefone = requisicao.Telefone,
                CpfCnpj = requisicao.CpfCnpj,
                Uf = requisicao.Uf,
                Empresa = requisicao.Empresa,
                ResponsavelId = responsavelId,
                UsuarioId = usuarioId,
            },
            ct);

        // Lead de outra atendente: nem o id sai na resposta, como no 404 da ficha.
        var podeAbrir = await visibilidade.Visiveis().AnyAsync(l => l.Id == processada.LeadId, ct);
        var resposta = new CriarLeadResponse(podeAbrir ? processada.LeadId : null, processada.Resultado, podeAbrir);
        return processada.Resultado == ResultadoEntrada.Criado
            ? CreatedAtAction(nameof(Obter), new { id = processada.LeadId }, resposta)
            : resposta;
    }

    /// <summary>
    /// Edita os dados pessoais. O formato de e-mail, telefone e CPF/CNPJ só é conferido nos campos que
    /// mudaram: um valor inválido que veio da integração não impede salvar o resto.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<LeadResponse>> Editar(long id, EditarLeadRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        var lead = await visibilidade.Visiveis().SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null)
        {
            return NotFound();
        }

        var novos = DadosPessoa.Normalizar(
            requisicao.Nome, requisicao.Email, requisicao.Telefone, requisicao.CpfCnpj, requisicao.Uf, requisicao.Empresa);
        ConferirFormatosAlterados(lead, novos);
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var campos = CamposAlterados(lead, novos);
        if (campos.Count == 0)
        {
            return (await BuscarFichaAsync(id))!;
        }

        // A versão que a pessoa viu: se o lead mudou depois disso, o SaveChanges falha e responde 409.
        db.Entry(lead).Property(l => l.Versao).OriginalValue = requisicao.Versao;
        lead.Nome = novos.Nome;
        lead.Email = novos.Email;
        lead.Telefone = novos.Telefone;
        lead.CpfCnpj = novos.CpfCnpj;
        lead.Uf = novos.Uf;
        lead.Empresa = novos.Empresa;
        lead.AtualizadoEm = relogio.GetUtcNow();
        auditoria.Registrar(AcoesAuditoria.LeadEditado, EntidadesAuditoria.Lead, id.ToString(CultureInfo.InvariantCulture), new { campos });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(detail: MensagemConflito, statusCode: StatusCodes.Status409Conflict);
        }

        return (await BuscarFichaAsync(id))!;
    }

    /// <summary>
    /// Leva o lead para outra etapa (seção 3.4). Perdido exige o motivo, e Ganho pode exigir CPF/CNPJ válido.
    /// A mudança entra no histórico com quem mudou e quando. Ir para a etapa em que o lead já está não muda nada.
    /// </summary>
    [HttpPost("{id:long}/etapa")]
    public async Task<ActionResult<LeadResponse>> MudarEtapa(long id, MudarEtapaRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Travado antes de conferir se é visível: a leitura seguinte já vê a versão final do lead.
        await db.TravarLeadAsync(id, ct);
        var lead = await visibilidade.Visiveis().SingleOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null)
        {
            return NotFound();
        }

        if (await mudancaEtapa.MudarAsync(lead, requisicao, visibilidade.UsuarioId, ct) is { } erro)
        {
            if (erro.Campo is null)
            {
                return Problem(detail: erro.Mensagem, statusCode: StatusCodes.Status400BadRequest);
            }

            ModelState.AddModelError(erro.Campo, erro.Mensagem);
            return ValidationProblem(ModelState);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        return (await BuscarFichaAsync(id))!;
    }

    private IQueryable<Lead> Filtrar(IQueryable<Lead> consulta, FiltroLeads filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            consulta = Buscar(consulta, filtro.Busca);
        }

        if (filtro.EtapaId is { } etapaId)
        {
            consulta = consulta.Where(l => l.EtapaId == etapaId);
        }

        if (filtro.Origem is { } origem)
        {
            consulta = consulta.Where(l => l.Origem == origem);
        }

        if (filtro.CampanhaId is { } campanhaId)
        {
            consulta = consulta.Where(l => l.CampanhaId == campanhaId);
        }

        // A atendente já só vê os próprios leads; os filtros de responsável são do admin.
        if (visibilidade.EhAdmin && filtro.SemResponsavel)
        {
            consulta = consulta.Where(l => l.ResponsavelId == null);
        }
        else if (visibilidade.EhAdmin && filtro.ResponsavelId is { } responsavelId)
        {
            consulta = consulta.Where(l => l.ResponsavelId == responsavelId);
        }

        if (filtro.De is { } de)
        {
            var inicio = HorarioBrasilia.InicioDoDia(de);
            consulta = consulta.Where(l => l.CriadoEm >= inicio);
        }

        if (filtro.Ate is { } ate)
        {
            var fim = HorarioBrasilia.InicioDoDia(ate.AddDays(1));
            consulta = consulta.Where(l => l.CriadoEm < fim);
        }

        return consulta;
    }

    /// <summary>Nome e empresa sem acento, e-mail, e os dígitos no telefone e no CPF/CNPJ.</summary>
    private static IQueryable<Lead> Buscar(IQueryable<Lead> consulta, string busca)
    {
        const int MinimoDeCaracteresNoDocumento = 4;
        var termo = busca.Trim();
        var padrao = $"%{EscaparLike(termo)}%";
        var digitos = string.Concat(termo.Where(char.IsAsciiDigit));
        var documento = string.Concat(termo.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        var buscaTelefone = digitos.Length >= MinimoDeCaracteresNoDocumento;
        var buscaDocumento = documento.Length >= MinimoDeCaracteresNoDocumento;

        return consulta.Where(l =>
            EF.Functions.ILike(EF.Functions.Unaccent(l.Nome), EF.Functions.Unaccent(padrao))
            || (l.Empresa != null && EF.Functions.ILike(EF.Functions.Unaccent(l.Empresa), EF.Functions.Unaccent(padrao)))
            || (l.Email != null && EF.Functions.ILike(l.Email, padrao))
            || (buscaTelefone && l.Telefone != null && l.Telefone.Contains(digitos))
            || (buscaDocumento && l.CpfCnpj != null && l.CpfCnpj.Contains(documento)));
    }

    /// <summary>A barra invertida é o escape padrão do LIKE no PostgreSQL.</summary>
    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private void ConferirFormatosAlterados(Lead lead, DadosPessoa novos)
    {
        if (novos.Email != lead.Email && novos.Email is not null && !novos.EmailValido)
        {
            ModelState.AddModelError("email", RegrasLead.MensagemEmail);
        }

        if (novos.Telefone != lead.Telefone && novos.Telefone is not null && !novos.TelefoneValido)
        {
            ModelState.AddModelError("telefone", RegrasLead.MensagemTelefone);
        }

        if (novos.CpfCnpj != lead.CpfCnpj && novos.CpfCnpj is not null && !novos.DocumentoValido)
        {
            ModelState.AddModelError("cpfCnpj", RegrasLead.MensagemDocumento);
        }
    }

    /// <summary>Nomes dos campos que mudaram, para a auditoria (sem os valores).</summary>
    private static List<string> CamposAlterados(Lead lead, DadosPessoa novos)
    {
        var campos = new List<string>();
        void Comparar(string campo, string? atual, string? novo)
        {
            if (atual != novo)
            {
                campos.Add(campo);
            }
        }

        Comparar("nome", lead.Nome, novos.Nome);
        Comparar("email", lead.Email, novos.Email);
        Comparar("telefone", lead.Telefone, novos.Telefone);
        Comparar("cpfCnpj", lead.CpfCnpj, novos.CpfCnpj);
        Comparar("uf", lead.Uf, novos.Uf);
        Comparar("empresa", lead.Empresa, novos.Empresa);
        return campos;
    }

    private async Task<LeadResponse?> BuscarFichaAsync(long id)
    {
        var lead = await visibilidade.Visiveis()
            .AsNoTracking()
            .Include(l => l.Etapa)
            .Include(l => l.Responsavel)
            .Include(l => l.Campanha)
            .Include(l => l.MotivoPerda)
            .SingleOrDefaultAsync(l => l.Id == id, HttpContext.RequestAborted);
        return lead is null ? null : ParaFicha(lead);
    }

    private static LeadResponse ParaFicha(Lead lead) => new(
        lead.Id,
        lead.Nome,
        lead.Email,
        lead.Telefone,
        lead.CpfCnpj,
        lead.Uf,
        lead.Empresa,
        new EtapaResumo(lead.Etapa.Id, lead.Etapa.Nome, lead.Etapa.Tipo),
        lead.Responsavel is null ? null : new ResponsavelResumo(lead.Responsavel.Id, lead.Responsavel.Nome),
        lead.Origem,
        lead.Plataforma,
        lead.Campanha is null ? null : new CampanhaResumo(lead.Campanha.Id, lead.Campanha.Nome),
        lead.UtmSource,
        lead.UtmMedium,
        lead.UtmCampaign,
        lead.UtmContent,
        lead.UtmTerm,
        lead.Gclid,
        RespostasExtras(lead.RespostasExtras),
        lead.Teste,
        lead.CriadoEm,
        lead.AtualizadoEm,
        lead.PrimeiroContatoEm,
        lead.ConvertidoEm,
        lead.PerdidoEm,
        lead.MotivoPerda is null ? null : new MotivoPerdaResumo(lead.MotivoPerda.Id, lead.MotivoPerda.Nome),
        lead.MotivoPerdaDetalhe,
        lead.AnonimizadoEm,
        AvisosLead.Calcular(lead.Email, lead.Telefone, lead.CpfCnpj),
        lead.Versao);

    private static List<RespostaExtra> RespostasExtras(JsonDocument? respostas) =>
        respostas?.RootElement.ValueKind == JsonValueKind.Object
            ? [.. respostas.RootElement.EnumerateObject().Select(p => new RespostaExtra(
                p.Name, p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText()))]
            : [];
}
