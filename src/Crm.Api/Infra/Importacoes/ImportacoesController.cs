using System.Globalization;
using System.Text;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Features.Usuarios;
using Crm.Api.Infra.Auditoria;
using Crm.Api.Infra.Dados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Infra.Importacoes;

/// <summary>
/// Importação de leads por CSV (seção 3.7), só para admin. A análise lê o arquivo e sugere o mapeamento sem gravar
/// nada; a criação grava a importação, que é processada em segundo plano, linha a linha, pelo mesmo serviço de entrada
/// da integração e do cadastro manual.
/// </summary>
[ApiController]
[Route("api/importacoes")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ImportacoesController(
    CrmDbContext db,
    VisibilidadeLeads visibilidade,
    AuditoriaService auditoria,
    FilaImportacoes fila,
    TimeProvider relogio) : ControllerBase
{
    public const string MensagemArquivoIncompleto = "O arquivo não chegou inteiro. Envie de novo.";

    public const string MensagemColunas = "O mapeamento não bate com as colunas do arquivo. Envie o arquivo de novo.";

    public const string MensagemColunaIdExterno = "A coluna do id externo não existe no arquivo.";

    public const string MensagemCampanha = "Campanha não encontrada para essa origem.";

    /// <summary>Quantos erros a resposta traz. O relatório de erros traz todos.</summary>
    public const int ErrosNaResposta = 100;

    /// <summary>O arquivo de 5 MB em base64, com folga para o resto do JSON.</summary>
    private const int TamanhoMaximoCorpo = 8 * 1024 * 1024;

    /// <summary>Tamanho do arquivo de 5 MB em base64.</summary>
    private const int TamanhoMaximoBase64 = (ArquivoCsv.TamanhoMaximo + 2) / 3 * 4;

    private const int TamanhoPrevia = 10;

    /// <summary>Lê o arquivo e devolve a prévia e a sugestão de mapeamento. Não grava nada.</summary>
    [HttpPost("analise")]
    [RequestSizeLimit(TamanhoMaximoCorpo)]
    public ActionResult<AnaliseImportacaoResponse> Analisar(AnalisarImportacaoRequest requisicao)
    {
        if (LerArquivo(requisicao.ConteudoBase64) is not (_, { } tabela))
        {
            return ValidationProblem(ModelState);
        }

        var sugestao = SugestaoColunas.Sugerir(tabela);
        return new AnaliseImportacaoResponse(
            requisicao.NomeArquivo.Trim(),
            tabela.Codificacao,
            tabela.Separador,
            [.. tabela.Colunas.Zip(sugestao.Campos, (nome, campo) => new ColunaAnalisada(nome, campo))],
            [.. tabela.Linhas.Take(TamanhoPrevia).Select(l => l.Valores)],
            tabela.Linhas.Count,
            sugestao.ColunaIdExterno,
            sugestao.Origem);
    }

    /// <summary>
    /// Grava a importação e a põe na fila. As linhas são processadas em segundo plano: acompanhe pelo
    /// <c>GET /api/importacoes/{id}</c>. No fim, quem importou recebe o resumo, e cada responsável, quantos leads ganhou.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(TamanhoMaximoCorpo)]
    [ProducesResponseType<ImportacaoResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<ImportacaoResponse>> Criar(CriarImportacaoRequest requisicao)
    {
        var ct = HttpContext.RequestAborted;
        if (LerArquivo(requisicao.ConteudoBase64) is not ({ } arquivo, { } tabela))
        {
            return ValidationProblem(ModelState);
        }

        if (requisicao.Campos.Count != tabela.Colunas.Count)
        {
            ModelState.AddModelError("campos", MensagemColunas);
        }

        if (requisicao.ColunaIdExterno >= tabela.Colunas.Count)
        {
            ModelState.AddModelError("colunaIdExterno", MensagemColunaIdExterno);
        }

        var campanhaPadrao = await CampanhaPadraoAsync(requisicao, ct);
        if (requisicao.ResponsavelId is { } responsavelId && !await db.Users.AnyAsync(u => u.Id == responsavelId && u.Ativo, ct))
        {
            ModelState.AddModelError("responsavelId", LeadsController.MensagemResponsavelInvalido);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var agora = relogio.GetUtcNow();
        var importacao = new Importacao
        {
            UsuarioId = visibilidade.UsuarioId!.Value,
            NomeArquivo = requisicao.NomeArquivo.Trim(),
            Codificacao = tabela.Codificacao,
            Separador = tabela.Separador,
            Origem = requisicao.Origem,
            Campos = [.. requisicao.Campos],
            ColunaIdExterno = requisicao.ColunaIdExterno,
            CampanhaPadraoId = campanhaPadrao?.Id,
            CampanhaPadraoNome = campanhaPadrao?.Nome,
            ResponsavelId = requisicao.ResponsavelId,
            Arquivo = arquivo,
            Situacao = SituacaoImportacao.Processando,
            TotalLinhas = tabela.Linhas.Count,
            ReservadaAte = agora + ProcessadorImportacao.Reserva,
            CriadoEm = agora,
        };

        await using (var transacao = await db.Database.BeginTransactionAsync(ct))
        {
            db.Importacoes.Add(importacao);
            await db.SaveChangesAsync(ct);
            auditoria.Registrar(
                AcoesAuditoria.ImportacaoCriada,
                EntidadesAuditoria.Importacao,
                importacao.Id.ToString(CultureInfo.InvariantCulture),
                new { origem = EnumTexto<Origem>.Nome(importacao.Origem), linhas = importacao.TotalLinhas });
            await db.SaveChangesAsync(ct);
            await transacao.CommitAsync(ct);
        }

        fila.Enfileirar(importacao.Id);
        return AcceptedAtAction(nameof(Obter), new { id = importacao.Id }, await RespostaAsync(importacao.Id, ct));
    }

    /// <summary>O andamento e, no fim, o resultado da importação, com as 100 primeiras linhas com erro.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportacaoResponse>> Obter(long id) =>
        await RespostaAsync(id, HttpContext.RequestAborted) is { } resposta ? resposta : NotFound();

    /// <summary>
    /// Todas as linhas com erro, em CSV para o Excel (UTF-8 com BOM e ponto e vírgula): a linha da planilha e o
    /// motivo, sem dados pessoais.
    /// </summary>
    [HttpGet("{id:long}/erros")]
    [ProducesResponseType<string>(StatusCodes.Status200OK, "text/csv")]
    public async Task<IActionResult> Erros(long id)
    {
        var ct = HttpContext.RequestAborted;
        if (!await db.Importacoes.AnyAsync(i => i.Id == id, ct))
        {
            return NotFound();
        }

        var erros = await db.ErrosImportacao
            .Where(e => e.ImportacaoId == id)
            .OrderBy(e => e.Linha)
            .ThenBy(e => e.Id)
            .Select(e => new ErroImportacaoResponse(e.Linha, e.Motivo))
            .ToListAsync(ct);

        var csv = new StringBuilder("Linha;Motivo\r\n");
        foreach (var erro in erros)
        {
            csv.Append(erro.Linha.ToString(CultureInfo.InvariantCulture)).Append(';').Append(CampoCsv(erro.Motivo)).Append("\r\n");
        }

        return File(
            [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())],
            "text/csv; charset=utf-8",
            $"importacao-{id.ToString(CultureInfo.InvariantCulture)}-erros.csv");
    }

    /// <summary>O arquivo e a leitura dele, ou nulo com o motivo no campo <c>arquivo</c> do ModelState.</summary>
    private (byte[] Arquivo, TabelaCsv Tabela)? LerArquivo(string? base64)
    {
        if (base64?.Length > TamanhoMaximoBase64)
        {
            ModelState.AddModelError("arquivo", ArquivoCsv.MensagemTamanho);
            return null;
        }

        byte[] arquivo;
        try
        {
            arquivo = Convert.FromBase64String(base64 ?? string.Empty);
        }
        catch (FormatException)
        {
            ModelState.AddModelError("arquivo", MensagemArquivoIncompleto);
            return null;
        }

        var leitura = ArquivoCsv.Ler(arquivo);
        if (!leitura.Lido)
        {
            ModelState.AddModelError("arquivo", leitura.Erro);
            return null;
        }

        return (arquivo, leitura.Tabela);
    }

    /// <summary>
    /// A campanha das linhas sem campanha: uma que já existe, da mesma origem, ou uma nova, cujo nome serve de id. A
    /// nova só é criada quando a primeira linha a usa.
    /// </summary>
    private async Task<ReferenciaExterna?> CampanhaPadraoAsync(CriarImportacaoRequest requisicao, CancellationToken ct)
    {
        if (requisicao.CampanhaPadraoId is { } campanhaId)
        {
            var idExterno = await db.Campanhas
                .Where(c => c.Id == campanhaId && c.Origem == requisicao.Origem)
                .Select(c => c.IdExterno)
                .SingleOrDefaultAsync(ct);
            if (idExterno is null)
            {
                ModelState.AddModelError("campanhaPadraoId", MensagemCampanha);
            }

            return idExterno is null ? null : new ReferenciaExterna(idExterno, null);
        }

        var nome = Textos.Limpar(requisicao.NovaCampanha);
        return nome is null ? null : new ReferenciaExterna(nome, nome);
    }

    /// <summary>A importação, sem o arquivo: a tela consulta a cada segundo enquanto ela processa.</summary>
    private async Task<ImportacaoResponse?> RespostaAsync(long id, CancellationToken ct)
    {
        var resposta = await db.Importacoes
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ImportacaoResponse(
                i.Id,
                i.NomeArquivo,
                i.Codificacao,
                i.Separador,
                i.Origem,
                db.Campanhas
                    .Where(c => c.Origem == i.Origem && c.IdExterno == i.CampanhaPadraoId)
                    .Select(c => c.Nome)
                    .SingleOrDefault() ?? i.CampanhaPadraoNome,
                db.Users.Where(u => u.Id == i.ResponsavelId).Select(u => new ResponsavelResumo(u.Id, u.Nome)).SingleOrDefault(),
                db.Users.Where(u => u.Id == i.UsuarioId).Select(u => u.Nome).Single(),
                i.Situacao,
                i.TotalLinhas,
                i.LinhasProcessadas,
                i.Criados,
                i.Vinculados,
                i.JaImportados,
                i.ComErro,
                i.SemResponsavel,
                new List<ErroImportacaoResponse>(),
                i.CriadoEm,
                i.ConcluidoEm))
            .SingleOrDefaultAsync(ct);
        if (resposta is null)
        {
            return null;
        }

        var erros = await db.ErrosImportacao
            .Where(e => e.ImportacaoId == id)
            .OrderBy(e => e.Linha)
            .ThenBy(e => e.Id)
            .Take(ErrosNaResposta)
            .Select(e => new ErroImportacaoResponse(e.Linha, e.Motivo))
            .ToListAsync(ct);
        return resposta with { Erros = erros };
    }

    /// <summary>Um campo do CSV entre aspas quando tem o separador, aspas ou quebra de linha.</summary>
    private static string CampoCsv(string valor) =>
        valor.AsSpan().IndexOfAny(";\"\r\n") < 0 ? valor : $"\"{valor.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

internal static class ImportacoesExtensions
{
    /// <summary>A fila desta instância, o processador e o serviço que processa as importações em segundo plano.</summary>
    public static IServiceCollection AddImportacoes(this IServiceCollection services)
    {
        services.AddSingleton<FilaImportacoes>();
        services.AddHostedService<ServicoImportacoes>();
        return services.AddScoped<ProcessadorImportacao>();
    }
}
