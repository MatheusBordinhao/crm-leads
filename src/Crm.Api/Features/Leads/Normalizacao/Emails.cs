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

    /// <summary>O valor já normalizado tem o formato de um e-mail.</summary>
    public static bool Valido(string? normalizado) =>
        normalizado is not null && normalizado.Length <= TamanhoMaximo && Formato().IsMatch(normalizado);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Formato();
}
