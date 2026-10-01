using System.Text;
using System.Text.Json.Serialization;

namespace Crm.Api.Infra.Importacoes;

/// <summary>Codificação reconhecida no arquivo da importação (seção 3.7). Os valores começam em 1.</summary>
public enum Codificacao
{
    [JsonStringEnumMemberName("utf-8")]
    Utf8 = 1,

    /// <summary>Little ou big endian. O arquivo exportado pela Meta costuma vir assim, com tab.</summary>
    [JsonStringEnumMemberName("utf-16")]
    Utf16 = 2,

    /// <summary>O "CSV (separado por vírgulas)" do Excel brasileiro, que usa ponto e vírgula.</summary>
    [JsonStringEnumMemberName("windows-1252")]
    Windows1252 = 3,
}

/// <summary>
/// Descobre a codificação do arquivo e o transforma em texto. Com BOM, vale o BOM. Sem ele, o UTF-16 é reconhecido
/// pelos bytes zero dos caracteres comuns; o resto é UTF-8, se os bytes forem UTF-8 válido, ou Windows-1252.
/// </summary>
internal static class Codificacoes
{
    /// <summary>Quantos bytes do início bastam para reconhecer o UTF-16 sem BOM.</summary>
    private const int AmostraUtf16 = 4096;

    private static readonly UTF8Encoding Utf8Estrito = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly Encoding Windows1252 = CriarWindows1252();

    private static ReadOnlySpan<byte> BomUtf8 => [0xEF, 0xBB, 0xBF];

    private static ReadOnlySpan<byte> BomUtf16LittleEndian => [0xFF, 0xFE];

    private static ReadOnlySpan<byte> BomUtf16BigEndian => [0xFE, 0xFF];

    public static (string Texto, Codificacao Codificacao) Decodificar(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(BomUtf8))
        {
            return (Encoding.UTF8.GetString(bytes[BomUtf8.Length..]), Codificacao.Utf8);
        }

        if (bytes.StartsWith(BomUtf16LittleEndian))
        {
            return (Encoding.Unicode.GetString(bytes[BomUtf16LittleEndian.Length..]), Codificacao.Utf16);
        }

        if (bytes.StartsWith(BomUtf16BigEndian))
        {
            return (Encoding.BigEndianUnicode.GetString(bytes[BomUtf16BigEndian.Length..]), Codificacao.Utf16);
        }

        if (Utf16SemBom(bytes) is { } utf16)
        {
            return (utf16.GetString(bytes), Codificacao.Utf16);
        }

        try
        {
            return (Utf8Estrito.GetString(bytes), Codificacao.Utf8);
        }
        catch (DecoderFallbackException)
        {
            return (Windows1252.GetString(bytes), Codificacao.Windows1252);
        }
    }

    /// <summary>
    /// UTF-16 sem BOM. Nas letras do português, um dos dois bytes de cada caractere é zero: o segundo em little
    /// endian, o primeiro em big endian. UTF-8 e Windows-1252 não têm bytes zero. Nulo quando não há esse padrão.
    /// </summary>
    private static Encoding? Utf16SemBom(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes.Length % 2 != 0)
        {
            return null;
        }

        var amostra = bytes[..Math.Min(bytes.Length, AmostraUtf16)];
        int zerosNoPrimeiro = 0, zerosNoSegundo = 0;
        for (var i = 0; i + 1 < amostra.Length; i += 2)
        {
            zerosNoPrimeiro += amostra[i] == 0 ? 1 : 0;
            zerosNoSegundo += amostra[i + 1] == 0 ? 1 : 0;
        }

        var caracteres = amostra.Length / 2;
        if (zerosNoSegundo > caracteres * 0.3 && zerosNoPrimeiro < caracteres * 0.05)
        {
            return Encoding.Unicode;
        }

        return zerosNoPrimeiro > caracteres * 0.3 && zerosNoSegundo < caracteres * 0.05 ? Encoding.BigEndianUnicode : null;
    }

    /// <summary>O .NET só traz o Windows-1252 depois de registrar os code pages do Windows.</summary>
    private static Encoding CriarWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
