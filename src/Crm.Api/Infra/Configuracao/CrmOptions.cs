using System.Globalization;
using Microsoft.Extensions.Options;

namespace Crm.Api.Infra.Configuracao;

/// <summary>Configuração geral do CRM, na seção <c>Crm</c>.</summary>
public sealed class CrmOptions
{
    public const string Secao = "Crm";

    /// <summary>
    /// Endereço do CRM para os links que saem do sistema, como o da ficha nos eventos do n8n. Em desenvolvimento,
    /// o padrão é o front local (<c>appsettings.Development.json</c>).
    /// </summary>
    public string? UrlPublica { get; set; }

    public string LinkDoLead(long id) =>
        $"{UrlPublica!.TrimEnd('/')}/leads/{id.ToString(CultureInfo.InvariantCulture)}";
}

internal sealed class ValidacaoCrmOptions : IValidateOptions<CrmOptions>
{
    public const string MensagemUrlPublica =
        "Configure Crm__UrlPublica com o endereço do CRM, como https://crm.empresa.com.br (veja o .env.example).";

    public ValidateOptionsResult Validate(string? name, CrmOptions options) =>
        UrlAbsoluta(options.UrlPublica) ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(MensagemUrlPublica);

    /// <summary>Endereço completo, com http ou https.</summary>
    public static bool UrlAbsoluta(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
