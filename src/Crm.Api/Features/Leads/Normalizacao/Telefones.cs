using PhoneNumbers;

namespace Crm.Api.Features.Leads.Normalizacao;

/// <summary>Telefone em E.164 (<c>+5551999998888</c>), reconhecido pelo libphonenumber com região padrão BR.</summary>
public static class Telefones
{
    private const string RegiaoPadrao = "BR";

    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// E.164 quando o número é reconhecido; senão, o texto como veio, sem espaços nas pontas. Vazio vira nulo.
    /// </summary>
    public static string? Normalizar(string? valor)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            return null;
        }

        return TentarE164(texto, out var e164) ? e164 : texto;
    }

    /// <summary>O valor já normalizado é um número reconhecido, em E.164.</summary>
    public static bool Valido(string? normalizado) =>
        normalizado is not null && TentarE164(normalizado, out var e164) && e164 == normalizado;

    private static bool TentarE164(string texto, out string e164)
    {
        e164 = string.Empty;
        try
        {
            var numero = Util.Parse(texto, RegiaoPadrao);
            if (!Util.IsValidNumber(numero))
            {
                return false;
            }

            e164 = Util.Format(numero, PhoneNumberFormat.E164);
            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
