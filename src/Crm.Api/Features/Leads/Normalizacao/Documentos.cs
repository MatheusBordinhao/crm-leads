namespace Crm.Api.Features.Leads.Normalizacao;

/// <summary>
/// CPF e CNPJ (seção 3.1), guardados como texto: só letras e números, em maiúsculas. 11 caracteres é CPF; 14 é
/// CNPJ, numérico ou alfanumérico (desde julho de 2026).
/// </summary>
public static class Documentos
{
    private static readonly int[] PesosPrimeiroDigitoCnpj = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    private static readonly int[] PesosSegundoDigitoCnpj = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>Tira máscara, espaços e qualquer outro símbolo e passa para maiúsculas. Vazio vira nulo.</summary>
    public static string? Normalizar(string? valor)
    {
        if (valor is null)
        {
            return null;
        }

        var normalizado = string.Concat(valor.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant));
        return normalizado.Length == 0 ? null : normalizado;
    }

    /// <summary>
    /// O documento guardado com a máscara, como uma pessoa lê numa planilha: <c>529.982.247-25</c>, <c>11.222.333/0001-81</c>
    /// e <c>12.ABC.345/01DE-35</c>. Vale pelo tamanho de um valor só de letras e números; outro valor sai como foi guardado.
    /// </summary>
    public static string? ParaLeitura(string? normalizado) => normalizado switch
    {
        { Length: 11 } when normalizado.All(char.IsAsciiLetterOrDigit) =>
            $"{normalizado[..3]}.{normalizado[3..6]}.{normalizado[6..9]}-{normalizado[9..]}",
        { Length: 14 } when normalizado.All(char.IsAsciiLetterOrDigit) =>
            $"{normalizado[..2]}.{normalizado[2..5]}.{normalizado[5..8]}/{normalizado[8..12]}-{normalizado[12..]}",
        _ => normalizado,
    };

    /// <summary>Confere os dígitos verificadores de um valor já normalizado.</summary>
    public static bool Valido(string? normalizado) => normalizado?.Length switch
    {
        11 => CpfValido(normalizado),
        14 => CnpjValido(normalizado),
        _ => false,
    };

    private static bool CpfValido(string cpf)
    {
        // Sequências como 111.111.111-11 passam na conta, mas não são CPFs.
        if (!cpf.All(char.IsAsciiDigit) || cpf.All(c => c == cpf[0]))
        {
            return false;
        }

        return DigitoCpf(cpf, 9) == cpf[9] - '0' && DigitoCpf(cpf, 10) == cpf[10] - '0';
    }

    /// <summary>Pesos de <c>quantidade + 1</c> até 2 sobre os primeiros <paramref name="quantidade"/> dígitos.</summary>
    private static int DigitoCpf(string cpf, int quantidade)
    {
        var soma = 0;
        for (var i = 0; i < quantidade; i++)
        {
            soma += (cpf[i] - '0') * (quantidade + 1 - i);
        }

        return Digito(soma);
    }

    private static bool CnpjValido(string cnpj)
    {
        var raizValida = cnpj[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c));
        if (!raizValida || !cnpj[12..].All(char.IsAsciiDigit) || cnpj.All(c => c == cnpj[0]))
        {
            return false;
        }

        return DigitoCnpj(cnpj, PesosPrimeiroDigitoCnpj) == cnpj[12] - '0'
            && DigitoCnpj(cnpj, PesosSegundoDigitoCnpj) == cnpj[13] - '0';
    }

    /// <summary>Cada caractere vale o código ASCII menos 48: os números valem eles mesmos, A vale 17 e Z vale 42.</summary>
    private static int DigitoCnpj(string cnpj, int[] pesos)
    {
        var soma = 0;
        for (var i = 0; i < pesos.Length; i++)
        {
            soma += (cnpj[i] - 48) * pesos[i];
        }

        return Digito(soma);
    }

    /// <summary>Resto da divisão por 11 menor que 2 dá 0; senão, 11 menos o resto.</summary>
    private static int Digito(int soma)
    {
        var resto = soma % 11;
        return resto < 2 ? 0 : 11 - resto;
    }
}
