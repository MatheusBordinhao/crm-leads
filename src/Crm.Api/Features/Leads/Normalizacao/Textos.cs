namespace Crm.Api.Features.Leads.Normalizacao;

/// <summary>Textos livres do lead (nome, empresa, UTMs).</summary>
public static class Textos
{
    /// <summary>Sem espaços nas pontas e com um espaço só entre as palavras. Vazio vira nulo.</summary>
    public static string? Limpar(string? valor)
    {
        if (valor is null)
        {
            return null;
        }

        var limpo = string.Join(' ', valor.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return limpo.Length == 0 ? null : limpo;
    }

    /// <summary>Limpa e corta no tamanho máximo. Serve para a entrada da integração, que nunca é recusada por tamanho.</summary>
    public static string? LimparECortar(string? valor, int tamanhoMaximo) => Cortar(Limpar(valor), tamanhoMaximo)?.TrimEnd();

    public static string? Cortar(string? valor, int tamanhoMaximo) =>
        valor is null || valor.Length <= tamanhoMaximo ? valor : valor[..tamanhoMaximo];
}
