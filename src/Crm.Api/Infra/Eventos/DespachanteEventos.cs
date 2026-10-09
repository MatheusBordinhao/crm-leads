using System.Globalization;
using System.Text;
using Crm.Api.Features.Notificacoes;
using Crm.Api.Infra.Dados;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Eventos;

/// <summary>
/// Entrega ao n8n os eventos pendentes (seção 4.3), fora das requisições dos usuários. Cada evento vencido é
/// reservado por um minuto, para outro despachante não pegá-lo, e enviado fora da transação. Qualquer resposta
/// 2xx conta como entregue. Se falhar, o evento é tentado de novo depois das esperas de <see cref="RegrasDespacho"/>;
/// depois da última, fica como <c>falhou</c> e os admins são avisados. A entrega é "pelo menos uma vez", sem
/// garantia de ordem. Um evento apagado durante o envio, pela anonimização do lead, é deixado de lado.
/// </summary>
public sealed class DespachanteEventos(
    CrmDbContext db,
    IHttpClientFactory clientes,
    IOptions<N8nOptions> opcoes,
    Notificador notificador,
    TimeProvider relogio,
    ILogger<DespachanteEventos> logger)
{
    public const string NomeCliente = "n8n";

    public const string CabecalhoChave = "X-Crm-Chave";

    public const string CabecalhoEventoId = "X-Crm-Evento-Id";

    /// <summary>Por quanto tempo um evento fica reservado enquanto é enviado: mais que o tempo limite do envio.</summary>
    public static readonly TimeSpan Reserva = TimeSpan.FromMinutes(1);

    /// <summary>Quantos eventos cada rodada tenta, no máximo, antes de dar a vez para a próxima.</summary>
    public const int MaximoPorRodada = 50;

    /// <summary>
    /// Entrega os eventos vencidos, um de cada vez, e devolve quantos tentou. Sem <c>N8n__WebhookUrl</c>, não envia
    /// nada, e os eventos ficam pendentes.
    /// </summary>
    public async Task<int> DespacharAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opcoes.Value.WebhookUrl))
        {
            return 0;
        }

        var tentados = 0;
        while (tentados < MaximoPorRodada && await ReservarAsync(ct) is { } evento)
        {
            await EntregarAsync(evento, ct);
            tentados++;
        }

        return tentados;
    }

    /// <summary>O evento vencido mais antigo, já reservado. A reserva é um comando só, na sua própria transação.</summary>
    private async Task<EventoSaida?> ReservarAsync(CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        var reservadoAte = agora + Reserva;
        var reservados = await db.EventosSaida
            .FromSql($"""
                UPDATE eventos_saida SET proxima_tentativa_em = {reservadoAte}
                WHERE id = (
                    SELECT id FROM eventos_saida
                    WHERE situacao = 'pendente' AND proxima_tentativa_em <= {agora}
                    ORDER BY proxima_tentativa_em
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(ct);
        return reservados.SingleOrDefault();
    }

    private async Task EntregarAsync(EventoSaida evento, CancellationToken ct)
    {
        var erro = await EnviarAsync(evento, ct);
        var agora = relogio.GetUtcNow();
        evento.Tentativas++;
        evento.UltimoErro = erro;
        if (erro is null)
        {
            evento.Situacao = SituacaoEvento.Entregue;
            evento.EntregueEm = agora;
            logger.LogInformation(
                "Evento {EventoId} ({Tipo}) entregue ao n8n na tentativa {Tentativa}", evento.Id, evento.Tipo, evento.Tentativas);
        }
        else if (RegrasDespacho.EsperaDepoisDe(evento.Tentativas) is { } espera)
        {
            evento.ProximaTentativaEm = agora + espera;
            logger.LogWarning(
                "Evento {EventoId} ({Tipo}) não foi entregue ao n8n na tentativa {Tentativa}: {Erro}. Próxima tentativa em {ProximaTentativaEm}",
                evento.Id,
                evento.Tipo,
                evento.Tentativas,
                erro,
                evento.ProximaTentativaEm);
        }
        else
        {
            evento.Situacao = SituacaoEvento.Falhou;
            logger.LogError(
                "Evento {EventoId} ({Tipo}) falhou de vez depois de {Tentativas} tentativas: {Erro}", evento.Id, evento.Tipo, evento.Tentativas, erro);
            var tipo = EnumTexto<TipoEvento>.Nome(evento.Tipo);
            await notificador.ParaAdminsAsync(
                TipoNotificacao.EventoFalhou,
                $"O aviso {tipo} não chegou ao n8n depois de {evento.Tentativas} tentativas ({erro})",
                evento.LeadId,
                autorId: null,
                ct);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A anonimização do lead (LGPD) apaga os eventos dele, inclusive um que estava a caminho do n8n: não há mais o
            // que atualizar, nem um aviso de falha a dar.
            db.ChangeTracker.Clear();
            logger.LogInformation("Evento {EventoId} ({Tipo}) foi apagado durante o envio", evento.Id, evento.Tipo);
        }
    }

    /// <summary>
    /// Envia o evento e devolve o erro, ou nulo quando o n8n respondeu 2xx. O erro é só o código HTTP ou o tipo da
    /// falha: o corpo da resposta pode repetir o que foi enviado, que tem dados pessoais.
    /// </summary>
    private async Task<string?> EnviarAsync(EventoSaida evento, CancellationToken ct)
    {
        var n8n = opcoes.Value;
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, n8n.WebhookUrl)
        {
            Content = new StringContent(evento.Payload.RootElement.GetRawText(), Encoding.UTF8, "application/json"),
        };
        requisicao.Headers.Add(CabecalhoChave, n8n.WebhookChave);
        requisicao.Headers.Add(CabecalhoEventoId, evento.Id.ToString());

        try
        {
            using var resposta = await clientes.CreateClient(NomeCliente).SendAsync(requisicao, ct);
            return resposta.IsSuccessStatusCode
                ? null
                : $"HTTP {((int)resposta.StatusCode).ToString(CultureInfo.InvariantCulture)}";
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return $"Tempo esgotado ({n8n.TempoLimiteSegundos.ToString(CultureInfo.InvariantCulture)} s)";
        }
        catch (HttpRequestException excecao)
        {
            return $"Falha de conexão ({excecao.HttpRequestError})";
        }
    }
}
