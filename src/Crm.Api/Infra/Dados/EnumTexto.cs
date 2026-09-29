using System.Collections.Frozen;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crm.Api.Infra.Dados;

/// <summary>
/// Nomes dos enums de domínio como texto, os mesmos do JSON (<see cref="JsonStringEnumMemberNameAttribute"/>):
/// <c>meta_lead_ads</c>, <c>aberta</c>, <c>vinculado</c>. Servem para o banco e para a query string.
/// </summary>
internal static class EnumTexto<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> Nomes =
        Enum.GetValues<TEnum>().ToFrozenDictionary(valor => valor, NomeDoMembro);

    private static readonly FrozenDictionary<string, TEnum> Valores =
        Nomes.ToFrozenDictionary(par => par.Value, par => par.Key, StringComparer.Ordinal);

    public static IEnumerable<string> TodosOsNomes => Nomes.Values;

    public static string Nome(TEnum valor) =>
        Nomes.TryGetValue(valor, out var nome)
            ? nome
            : throw new ArgumentOutOfRangeException(nameof(valor), valor, $"Valor inválido para {typeof(TEnum).Name}.");

    public static bool TentarLer(string? texto, out TEnum valor) => Valores.TryGetValue(texto ?? string.Empty, out valor);

    public static TEnum Ler(string texto) =>
        TentarLer(texto, out var valor)
            ? valor
            : throw new InvalidOperationException($"Valor desconhecido para {typeof(TEnum).Name}: {texto}.");

    private static string NomeDoMembro(TEnum valor) =>
        typeof(TEnum).GetField(valor.ToString())?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
        ?? valor.ToString();
}

/// <summary>
/// Deixa a query string usar os mesmos nomes do JSON (<c>?origem=meta_lead_ads</c>). Os nomes do C# continuam
/// valendo.
/// </summary>
internal sealed class EnumTextoTypeConverter<TEnum>() : EnumConverter(typeof(TEnum))
    where TEnum : struct, Enum
{
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string texto && EnumTexto<TEnum>.TentarLer(texto.Trim(), out var valor)
            ? valor
            : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is TEnum valor
            ? EnumTexto<TEnum>.Nome(valor)
            : base.ConvertTo(context, culture, value, destinationType);
}

internal static class EnumTextoExtensions
{
    /// <summary>Grava o enum como texto, com o nome que ele tem no JSON.</summary>
    public static PropertyBuilder<TEnum> ComoTexto<TEnum>(this PropertyBuilder<TEnum> propriedade, int tamanhoMaximo = 30)
        where TEnum : struct, Enum =>
        propriedade
            .HasConversion(new ValueConverter<TEnum, string>(v => EnumTexto<TEnum>.Nome(v), t => EnumTexto<TEnum>.Ler(t)))
            .HasMaxLength(tamanhoMaximo);
}
