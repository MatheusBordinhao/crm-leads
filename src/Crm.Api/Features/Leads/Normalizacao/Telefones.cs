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

    /// <summary>
    /// O telefone guardado como uma pessoa lê, numa planilha: no formato nacional quando é do Brasil (<c>(51) 99999-8888</c>),
    /// no internacional quando é de outro país (<c>+1 650-253-0000</c>) e como foi guardado quando não foi reconhecido.
    /// </summary>
    public static string? ParaLeitura(string? normalizado)
    {
        if (!Valido(normalizado))
        {
            return normalizado;
        }

        var numero = Util.Parse(normalizado, RegiaoPadrao);
        var doBrasil = numero.CountryCode == Util.GetCountryCodeForRegion(RegiaoPadrao);
        return Util.Format(numero, doBrasil ? PhoneNumberFormat.NATIONAL : PhoneNumberFormat.INTERNATIONAL);
    }

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
