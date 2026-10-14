using System.Text.RegularExpressions;

namespace Crm.Api.Features.Leads.Normalizacao;

/// <summary>E-mail do lead: em minúsculas e sem espaços.</summary>
public static partial class Emails
{
    public const int TamanhoMaximo = 256;

    /// <summary>Tira todos os espaços e passa para minúsculas. Vazio vira nulo.</summary>
    public static string? Normalizar(string? valor)
    {
        if (valor is null)
        {
            return null;
        }

        var normalizado = string.Concat(valor.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();
        return normalizado.Length == 0 ? null : normalizado;
    }

    /// <summary>
    /// O valor já normalizado tem o formato de um e-mail (seção 3.1), o mesmo que a tela confere (<c>FORMATO_EMAIL</c>, em
    /// <c>web/src/features/leads/esquema.ts</c>): antes do @, só ASCII (letras, números e os símbolos do RFC 5322, com
    /// pontos só entre eles); o domínio, em rótulos de letras, números e hífen, separados por ponto. Um e-mail com acento,
    /// como <c>joão@exemplo.com</c>, quase nenhum provedor aceita: é erro de digitação.
    /// </summary>
    public static bool Valido(string? normalizado) =>
        normalizado is not null && normalizado.Length <= TamanhoMaximo && Formato().IsMatch(normalizado);

    [GeneratedRegex(
        @"^[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*"
        + @"@[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+\z")]
    private static partial Regex Formato();
}
