using System.ComponentModel;
using System.Text.Json.Serialization;
using Crm.Api.Infra.Dados;

namespace Crm.Api.Features.Leads;

/// <summary>De onde o lead veio (seção 3.2). Os valores começam em 1: o zero não é uma origem válida.</summary>
[TypeConverter(typeof(EnumTextoTypeConverter<Origem>))]
public enum Origem
{
    [JsonStringEnumMemberName("meta_lead_ads")]
    MetaLeadAds = 1,

    [JsonStringEnumMemberName("google_ads")]
    GoogleAds = 2,

    [JsonStringEnumMemberName("site")]
    Site = 3,

    [JsonStringEnumMemberName("whatsapp")]
    Whatsapp = 4,

    [JsonStringEnumMemberName("telefone")]
    Telefone = 5,

    [JsonStringEnumMemberName("indicacao")]
    Indicacao = 6,

    [JsonStringEnumMemberName("planilha")]
    Planilha = 7,

    [JsonStringEnumMemberName("manual")]
    Manual = 8,
}

/// <summary>Situação gravada em <c>entradas_recebidas</c>.</summary>
public enum SituacaoEntrada
{
    [JsonStringEnumMemberName("criado")]
    Criado = 1,

    [JsonStringEnumMemberName("vinculado")]
    Vinculado = 2,

    [JsonStringEnumMemberName("erro")]
    Erro = 3,
}

/// <summary>O que aconteceu com uma entrada: devolvido à integração e ao cadastro manual.</summary>
public enum ResultadoEntrada
{
    /// <summary>Pessoa nova: o lead foi criado.</summary>
    [JsonStringEnumMemberName("criado")]
    Criado = 1,

    /// <summary>A pessoa já era um lead: a entrada foi vinculada a ele.</summary>
    [JsonStringEnumMemberName("vinculado")]
    Vinculado = 2,

    /// <summary>A mesma origem e o mesmo id externo já tinham sido processados: nada mudou.</summary>
    [JsonStringEnumMemberName("ja_recebido")]
    JaRecebido = 3,
}

internal static class Origens
{
    /// <summary>Origens aceitas em <c>POST /api/integracoes/leads</c>.</summary>
    public static readonly Origem[] DaIntegracao = [Origem.MetaLeadAds, Origem.GoogleAds, Origem.Site];

    /// <summary>Origens que a atendente ou o admin escolhem ao cadastrar um lead à mão.</summary>
    public static readonly Origem[] DoCadastroManual = [Origem.Manual, Origem.Telefone, Origem.Whatsapp, Origem.Indicacao];

    /// <summary>Nome para mostrar, usado no nome provisório das campanhas.</summary>
    public static string NomeLegivel(Origem origem) => origem switch
    {
        Origem.MetaLeadAds => "Meta",
        Origem.GoogleAds => "Google Ads",
        Origem.Site => "Site",
        Origem.Whatsapp => "WhatsApp",
        Origem.Telefone => "Telefone",
        Origem.Indicacao => "Indicação",
        Origem.Planilha => "Planilha",
        Origem.Manual => "Cadastro manual",
        _ => origem.ToString(),
    };
}
