using System.Text.Json.Serialization;

namespace Crm.Api.Infra.Importacoes;

/// <summary>
/// Para onde vai cada coluna do arquivo na importação (seção 3.7). O nome aceita mais de uma coluna (nome e sobrenome);
/// os outros campos, uma só. Os valores começam em 1: o zero não é um campo válido.
/// </summary>
public enum CampoImportacao
{
    [JsonStringEnumMemberName("nao_importar")]
    NaoImportar = 1,

    [JsonStringEnumMemberName("nome")]
    Nome = 2,

    [JsonStringEnumMemberName("email")]
    Email = 3,

    [JsonStringEnumMemberName("telefone")]
    Telefone = 4,

    [JsonStringEnumMemberName("cpf_cnpj")]
    CpfCnpj = 5,

    [JsonStringEnumMemberName("uf")]
    Uf = 6,

    [JsonStringEnumMemberName("empresa")]
    Empresa = 7,

    [JsonStringEnumMemberName("campanha_id")]
    CampanhaId = 8,

    [JsonStringEnumMemberName("campanha_nome")]
    CampanhaNome = 9,

    /// <summary>Conjunto de anúncios da Meta ou grupo de anúncios do Google.</summary>
    [JsonStringEnumMemberName("conjunto_id")]
    ConjuntoId = 10,

    [JsonStringEnumMemberName("conjunto_nome")]
    ConjuntoNome = 11,

    [JsonStringEnumMemberName("anuncio_id")]
    AnuncioId = 12,

    [JsonStringEnumMemberName("anuncio_nome")]
    AnuncioNome = 13,

    [JsonStringEnumMemberName("formulario_id")]
    FormularioId = 14,

    [JsonStringEnumMemberName("formulario_nome")]
    FormularioNome = 15,

    [JsonStringEnumMemberName("plataforma")]
    Plataforma = 16,

    [JsonStringEnumMemberName("utm_source")]
    UtmSource = 17,

    [JsonStringEnumMemberName("utm_medium")]
    UtmMedium = 18,

    [JsonStringEnumMemberName("utm_campaign")]
    UtmCampaign = 19,

    [JsonStringEnumMemberName("utm_content")]
    UtmContent = 20,

    [JsonStringEnumMemberName("utm_term")]
    UtmTerm = 21,

    [JsonStringEnumMemberName("gclid")]
    Gclid = 22,

    /// <summary>Vai para as respostas extras do lead, com o nome da coluna como pergunta.</summary>
    [JsonStringEnumMemberName("resposta_extra")]
    RespostaExtra = 23,
}
