using Crm.Api.Features.Leads;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Crm.Api.Infra.Importacoes;

/// <summary>
/// Processa uma importação (seção 3.7). Cada linha passa pelo <see cref="ReceberEntrada"/>, num escopo próprio e sem
/// avisos por lead. O andamento é gravado a cada linha, com a reserva renovada: se a API parar no meio, outra rodada
/// continua de onde ela parou, e o id externo faz as linhas que já entraram responderem "já importado". No fim, as
/// contagens são refeitas a partir das entradas da importação, e o arquivo é apagado.
/// </summary>
public sealed class ProcessadorImportacao(
    CrmDbContext db,
    IServiceScopeFactory escopos,
    Notificador notificador,
    TimeProvider relogio,
    ILogger<ProcessadorImportacao> logger)
{
    /// <summary>Por quanto tempo a importação fica reservada a quem a processa. Cada linha renova a reserva.</summary>
    public static readonly TimeSpan Reserva = TimeSpan.FromMinutes(1);

    public const string ErroInesperado = "Não foi possível gravar a linha. Importe o arquivo de novo para tentar outra vez.";

    /// <summary>De quantas em quantas linhas o andamento vai para o log.</summary>
    private const int LinhasPorRegistroNoLog = 500;

    /// <summary>
    /// Processa as linhas que faltam e conclui a importação. Não faz nada se ela já foi concluída, por exemplo por
    /// outra rodada que a retomou.
    /// </summary>
    public async Task ProcessarAsync(long importacaoId, CancellationToken ct)
    {
        var importacao = await db.Importacoes
            .AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == importacaoId && i.Situacao == SituacaoImportacao.Processando, ct);
        if (importacao is null)
        {
            return;
        }

        // O arquivo foi lido no envio, pelo mesmo código: a leitura dá o mesmo resultado.
        var leitura = ArquivoCsv.Ler(importacao.Arquivo);
        if (!leitura.Lido)
        {
            throw new InvalidOperationException($"O arquivo da importação {importacaoId} não pôde ser lido de novo.");
        }

        var tabela = leitura.Tabela;
        var contexto = new ContextoImportacao(
            importacao.Id,
            importacao.UsuarioId,
            importacao.CriadoEm,
            importacao.Origem,
            tabela.Colunas,
            importacao.Campos,
            importacao.ColunaIdExterno,
            importacao.CampanhaPadraoId is null ? null : new ReferenciaExterna(importacao.CampanhaPadraoId, importacao.CampanhaPadraoNome),
            importacao.ResponsavelId);

        logger.LogInformation(
            "Importação {ImportacaoId}: processando {Total} linhas a partir da {Inicio}",
            importacao.Id,
            tabela.Linhas.Count,
            importacao.LinhasProcessadas + 1);
        for (var indice = importacao.LinhasProcessadas; indice < tabela.Linhas.Count; indice++)
        {
            var linha = tabela.Linhas[indice];
            var (resultado, erro) = await ProcessarLinhaAsync(linha, contexto, ct);
            await RegistrarLinhaAsync(importacao.Id, indice + 1, linha.Numero, resultado, erro, ct);
            if ((indice + 1) % LinhasPorRegistroNoLog == 0)
            {
                logger.LogInformation("Importação {ImportacaoId}: {Processadas} de {Total} linhas", importacao.Id, indice + 1, tabela.Linhas.Count);
            }
        }

        await ConcluirAsync(importacao, ct);
    }

    /// <summary>Retoma, uma de cada vez, as importações que ficaram com a reserva vencida porque a API parou no meio.</summary>
    public async Task RetomarInterrompidasAsync(CancellationToken ct)
    {
        while (await ReservarInterrompidaAsync(ct) is { } id)
        {
            logger.LogWarning("Importação {ImportacaoId} interrompida: continuando de onde parou", id);
            await ProcessarAsync(id, ct);
        }
    }

    /// <summary>
    /// A importação interrompida mais antiga, já reservada. A reserva é um comando só, e o <c>SKIP LOCKED</c> impede
    /// que duas rodadas peguem a mesma.
    /// </summary>
    private async Task<long?> ReservarInterrompidaAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var reservadaAte = agora + Reserva;
        var reservadas = await db.Database
            .SqlQuery<long>($"""
                UPDATE importacoes SET reservada_ate = {reservadaAte}
                WHERE id = (
                    SELECT id FROM importacoes
                    WHERE situacao = 'processando' AND reservada_ate <= {agora}
                    ORDER BY id
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING id AS "Value"
                """)
            .ToListAsync(ct);
        return reservadas.Count == 0 ? null : reservadas[0];
    }

    /// <summary>
    /// Passa a linha pelo serviço de entrada, num escopo próprio, e devolve o resultado ou o motivo do erro. Uma falha
    /// do banco (fora do ar, conexão perdida, impasse) interrompe a importação, que continua depois.
    /// </summary>
    private async Task<(ResultadoEntrada? Resultado, string? Erro)> ProcessarLinhaAsync(
        LinhaCsv linha, ContextoImportacao contexto, CancellationToken ct)
    {
        var montada = LinhaImportada.Montar(linha, contexto);
        if (montada.Entrada is not { } entrada)
        {
            return (null, montada.Erro);
        }

        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var processada = await escopo.ServiceProvider.GetRequiredService<ReceberEntrada>().ExecutarAsync(entrada, ct);
            return (processada.Resultado, null);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException && !FalhaPassageira(excecao))
        {
            // Só o tipo e a pilha: a mensagem de uma exceção pode trazer um valor da linha, que é dado pessoal.
            logger.LogError(
                "A linha {Linha} da importação {ImportacaoId} não foi gravada: {TipoDoErro}{NovaLinha}{Pilha}",
                linha.Numero,
                contexto.ImportacaoId,
                excecao.GetType().FullName,
                Environment.NewLine,
                excecao.StackTrace);
            return (null, ErroInesperado);
        }
    }

    private static bool FalhaPassageira(Exception excecao) =>
        excecao is TimeoutException
        || excecao is NpgsqlException { IsTransient: true }
        || excecao.InnerException is NpgsqlException { IsTransient: true } or TimeoutException;

    /// <summary>
    /// Grava o andamento e renova a reserva. O erro de uma linha entra na mesma transação do andamento, para uma
    /// rodada que retoma a importação não gravar o mesmo erro duas vezes.
    /// </summary>
    private async Task RegistrarLinhaAsync(
        long importacaoId, int processadas, int numero, ResultadoEntrada? resultado, string? erro, CancellationToken ct)
    {
        var reservadaAte = relogio.GetUtcNow() + Reserva;
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        if (erro is not null)
        {
            db.ErrosImportacao.Add(new ErroImportacao { ImportacaoId = importacaoId, Linha = numero, Motivo = erro });
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
        }

        var criado = resultado == ResultadoEntrada.Criado ? 1 : 0;
        var vinculado = resultado == ResultadoEntrada.Vinculado ? 1 : 0;
        var jaImportado = resultado == ResultadoEntrada.JaRecebido ? 1 : 0;
        var comErro = erro is null ? 0 : 1;
        await db.Importacoes
            .Where(i => i.Id == importacaoId)
            .ExecuteUpdateAsync(
                i => i
                    .SetProperty(x => x.LinhasProcessadas, processadas)
                    .SetProperty(x => x.Criados, x => x.Criados + criado)
                    .SetProperty(x => x.Vinculados, x => x.Vinculados + vinculado)
                    .SetProperty(x => x.JaImportados, x => x.JaImportados + jaImportado)
                    .SetProperty(x => x.ComErro, x => x.ComErro + comErro)
                    .SetProperty(x => x.ReservadaAte, reservadaAte),
                ct);
        await transacao.CommitAsync(ct);
    }

    /// <summary>
    /// Conclui a importação com as contagens refeitas a partir das entradas que ela gerou, que continuam certas mesmo
    /// se uma linha foi processada duas vezes, e apaga o arquivo. Só uma rodada conclui e avisa: quem importou recebe o
    /// resumo, e cada responsável, quantos leads novos ganhou.
    /// </summary>
    private async Task ConcluirAsync(Importacao importacao, CancellationToken ct)
    {
        var id = importacao.Id;
        var entradas = db.EntradasRecebidas.Where(e => e.ImportacaoId == id);
        var criadas = entradas.Where(e => e.Situacao == SituacaoEntrada.Criado);
        var criados = await criadas.CountAsync(ct);
        var vinculados = await entradas.CountAsync(e => e.Situacao == SituacaoEntrada.Vinculado, ct);
        var comErro = await db.ErrosImportacao.Where(e => e.ImportacaoId == id).Select(e => e.Linha).Distinct().CountAsync(ct);

        // Os leads novos de cada responsável; a chave nula conta os que ficaram sem responsável.
        var porResponsavel = await criadas
            .Join(db.Leads, e => e.LeadId, l => l.Id, (_, l) => l.ResponsavelId)
            .GroupBy(responsavelId => responsavelId)
            .Select(g => new { ResponsavelId = g.Key, Leads = g.Count() })
            .ToListAsync(ct);
        var semResponsavel = porResponsavel.Where(r => r.ResponsavelId is null).Sum(r => r.Leads);
        var jaImportados = importacao.TotalLinhas - criados - vinculados - comErro;
        var agora = relogio.GetUtcNow();

        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        var concluidas = await db.Importacoes
            .Where(i => i.Id == id && i.Situacao == SituacaoImportacao.Processando)
            .ExecuteUpdateAsync(
                i => i
                    .SetProperty(x => x.Situacao, SituacaoImportacao.Concluida)
                    .SetProperty(x => x.LinhasProcessadas, x => x.TotalLinhas)
                    .SetProperty(x => x.Criados, criados)
                    .SetProperty(x => x.Vinculados, vinculados)
                    .SetProperty(x => x.ComErro, comErro)
                    .SetProperty(x => x.JaImportados, x => x.TotalLinhas - criados - vinculados - comErro)
                    .SetProperty(x => x.SemResponsavel, semResponsavel)
                    .SetProperty(x => x.Arquivo, (byte[]?)null)
                    .SetProperty(x => x.ConcluidoEm, agora),
                ct);
        if (concluidas == 0)
        {
            return;
        }

        // O resumo vai para quem importou, mesmo sendo o autor da ação: a importação roda em segundo plano, e a pessoa
        // pode ter saído da tela (seção 3.7).
        notificador.ParaUsuario(
            importacao.UsuarioId,
            TipoNotificacao.ImportacaoConcluida,
            Resumo(criados, semResponsavel, vinculados, jaImportados, comErro),
            leadId: null,
            autorId: null,
            importacaoId: id);

        var responsaveis = porResponsavel.Where(r => r.ResponsavelId is not null).Select(r => r.ResponsavelId!.Value).ToList();
        var ativas = await db.Users.Where(u => responsaveis.Contains(u.Id) && u.Ativo).Select(u => u.Id).ToListAsync(ct);
        foreach (var responsavel in porResponsavel.Where(r => r.ResponsavelId is { } responsavelId && ativas.Contains(responsavelId)))
        {
            notificador.ParaUsuario(
                responsavel.ResponsavelId,
                TipoNotificacao.LeadsImportados,
                $"Você recebeu {Contar(responsavel.Leads, "lead", "leads")} da importação",
                leadId: null,
                autorId: importacao.UsuarioId);
        }

        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
        logger.LogInformation(
            "Importação {ImportacaoId} concluída: {Criados} criados, {Vinculados} vinculados, {JaImportados} já importados e {ComErro} com erro",
            id,
            criados,
            vinculados,
            jaImportados,
            comErro);
    }

    /// <summary>"Importação concluída: 12 leads novos (2 sem responsável), 1 vinculado, 0 já importados e 1 com erro".</summary>
    internal static string Resumo(int criados, int semResponsavel, int vinculados, int jaImportados, int comErro)
    {
        var novos = Contar(criados, "lead novo", "leads novos") + (semResponsavel > 0 ? $" ({semResponsavel} sem responsável)" : string.Empty);
        return $"Importação concluída: {novos}, {Contar(vinculados, "vinculado", "vinculados")}, "
            + $"{Contar(jaImportados, "já importado", "já importados")} e {comErro} com erro";
    }

    private static string Contar(int quantidade, string singular, string plural) =>
        $"{quantidade} {(quantidade == 1 ? singular : plural)}";
}
