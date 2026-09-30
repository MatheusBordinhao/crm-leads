using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Eventos;

/// <summary>Eventos para o n8n (seção 4.3), na seção <c>N8n</c> da configuração.</summary>
public sealed class N8nOptions
{
    public const string Secao = "N8n";

    public const int TamanhoMinimoChave = 32;

    /// <summary>Os tipos gravados quando <c>N8n__Eventos</c> não está configurada: os que o fluxo CRM - Alertas usa.</summary>
    public static readonly IReadOnlyList<TipoEvento> EventosPadrao =
        [TipoEvento.LeadCriado, TipoEvento.LeadReatribuido, TipoEvento.LeadEntradaRepetida];

    /// <summary>URL de produção do Webhook do fluxo CRM - Alertas. Vazia, o despachante não envia nada.</summary>
    public string? WebhookUrl { get; set; }

    /// <summary>Vai no cabeçalho <c>X-Crm-Chave</c>, conferido pelo Header Auth do Webhook do n8n.</summary>
    public string? WebhookChave { get; set; }

    /// <summary>
    /// Tipos de evento gravados, separados por vírgula. Cada envio gasta uma execução do plano do n8n, então só
    /// entram os que algum fluxo usa. Vazia, vale <see cref="EventosPadrao"/>.
    /// </summary>
    public string? Eventos { get; set; }

    public int TempoLimiteSegundos { get; set; } = 10;

    /// <summary>De quanto em quanto tempo o despachante confere a fila.</summary>
    public int IntervaloSegundos { get; set; } = 5;

    public bool Grava(TipoEvento tipo) => LerEventos(Eventos, out var tipos, out _) && tipos.Contains(tipo);

    /// <summary>
    /// Lê a lista de <c>N8n__Eventos</c>: nomes como <c>lead.criado</c>, separados por vírgula. Vazia, vale o
    /// padrão. Falso quando há um nome desconhecido, que volta em <paramref name="desconhecido"/>.
    /// </summary>
    public static bool LerEventos(string? lista, out IReadOnlySet<TipoEvento> tipos, out string? desconhecido)
    {
        desconhecido = null;
        if (string.IsNullOrWhiteSpace(lista))
        {
            tipos = EventosPadrao.ToHashSet();
            return true;
        }

        var lidos = new HashSet<TipoEvento>();
        foreach (var nome in lista.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!EnumTexto<TipoEvento>.TentarLer(nome, out var tipo))
            {
                desconhecido = nome;
                tipos = lidos;
                return false;
            }

            lidos.Add(tipo);
        }

        tipos = lidos;
        return true;
    }
}

/// <summary>Confere a configuração do n8n na subida da API, com mensagens que dizem o que corrigir.</summary>
internal sealed class ValidacaoN8nOptions : IValidateOptions<N8nOptions>
{
    public static readonly string MensagemChave =
        $"Configure N8n__WebhookChave com {N8nOptions.TamanhoMinimoChave} caracteres ou mais (veja o README).";

    public const string MensagemUrl = "N8n__WebhookUrl precisa ser um endereço completo, com https:// (veja o README).";

    public ValidateOptionsResult Validate(string? name, N8nOptions options)
    {
        var erros = new List<string>();
        if (!N8nOptions.LerEventos(options.Eventos, out _, out var desconhecido))
        {
            erros.Add(
                $"N8n__Eventos tem um tipo desconhecido: {desconhecido}. Use {string.Join(", ", EnumTexto<TipoEvento>.TodosOsNomes)}.");
        }

        var temUrl = !string.IsNullOrWhiteSpace(options.WebhookUrl);
        if (temUrl && !ValidacaoCrmOptions.UrlAbsoluta(options.WebhookUrl))
        {
            erros.Add(MensagemUrl);
        }

        var temChave = !string.IsNullOrEmpty(options.WebhookChave);
        if ((temUrl && !temChave) || (temChave && options.WebhookChave!.Length < N8nOptions.TamanhoMinimoChave))
        {
            erros.Add(MensagemChave);
        }

        if (options.TempoLimiteSegundos <= 0 || options.IntervaloSegundos <= 0)
        {
            erros.Add("N8n__TempoLimiteSegundos e N8n__IntervaloSegundos precisam ser maiores que zero.");
        }

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }
}
