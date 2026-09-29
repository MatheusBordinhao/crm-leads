using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace Crm.Api.Features.Leads.Normalizacao;

/// <summary>As 27 unidades da federação. A entrada aceita a sigla ou o nome do estado, com ou sem acento.</summary>
public static class Ufs
{
    private static readonly FrozenDictionary<string, string> NomesPorSigla = new Dictionary<string, string>
    {
        ["AC"] = "Acre",
        ["AL"] = "Alagoas",
        ["AP"] = "Amapá",
        ["AM"] = "Amazonas",
        ["BA"] = "Bahia",
        ["CE"] = "Ceará",
        ["DF"] = "Distrito Federal",
        ["ES"] = "Espírito Santo",
        ["GO"] = "Goiás",
        ["MA"] = "Maranhão",
        ["MT"] = "Mato Grosso",
        ["MS"] = "Mato Grosso do Sul",
        ["MG"] = "Minas Gerais",
        ["PA"] = "Pará",
        ["PB"] = "Paraíba",
        ["PR"] = "Paraná",
        ["PE"] = "Pernambuco",
        ["PI"] = "Piauí",
        ["RJ"] = "Rio de Janeiro",
        ["RN"] = "Rio Grande do Norte",
        ["RS"] = "Rio Grande do Sul",
        ["RO"] = "Rondônia",
        ["RR"] = "Roraima",
        ["SC"] = "Santa Catarina",
        ["SP"] = "São Paulo",
        ["SE"] = "Sergipe",
        ["TO"] = "Tocantins",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> SiglasPorNome =
        NomesPorSigla.ToFrozenDictionary(par => Simplificar(par.Value), par => par.Key);

    public static IEnumerable<string> Siglas => NomesPorSigla.Keys;

    /// <summary>Sigla em maiúsculas, a partir da sigla ou do nome do estado. Nulo se vier vazio ou não for reconhecida.</summary>
    public static string? Normalizar(string? valor)
    {
        var simplificado = Simplificar(valor ?? string.Empty);
        if (simplificado.Length == 0)
        {
            return null;
        }

        var sigla = simplificado.ToUpperInvariant();
        if (NomesPorSigla.ContainsKey(sigla))
        {
            return sigla;
        }

        return SiglasPorNome.GetValueOrDefault(simplificado);
    }

    public static bool Valida(string? sigla) => sigla is not null && NomesPorSigla.ContainsKey(sigla);

    /// <summary>Sem acentos, em minúsculas e com um espaço só entre as palavras.</summary>
    private static string Simplificar(string texto)
    {
        var semAcento = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(c);
            }
        }

        return string.Join(' ', semAcento.ToString().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
