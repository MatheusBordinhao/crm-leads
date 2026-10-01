using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using Crm.Api.Features.Leads;

namespace Crm.Api.Infra.Importacoes;

/// <summary>A sugestão feita pelo cabeçalho, que a tela mostra para o admin conferir e mudar.</summary>
/// <param name="Campos">O campo sugerido para cada coluna, na ordem do cabeçalho.</param>
/// <param name="ColunaIdExterno">A coluna com o id do lead na origem, quando o cabeçalho a indica.</param>
/// <param name="Origem">A origem que o arquivo indica: o exportado pela Meta é reconhecido; o resto é planilha.</param>
public sealed record SugestaoImportacao(IReadOnlyList<CampoImportacao> Campos, int? ColunaIdExterno, Origem Origem);

/// <summary>
/// Sugere o campo de cada coluna pelo nome dela, sem diferenciar acentos, maiúsculas e separadores
/// (<c>phone_number</c>, <c>Telefone</c>, <c>E-mail</c>). Uma coluna desconhecida vira resposta extra, e as colunas
/// técnicas da exportação da Meta não são importadas.
/// </summary>
internal static class SugestaoColunas
{
    /// <summary>Quantas linhas do começo bastam para reconhecer os ids da Meta.</summary>
    private const int LinhasConferidas = 10;

    private static readonly FrozenDictionary<string, CampoImportacao> CamposPorNome = new Dictionary<string, CampoImportacao>
    {
        ["nome"] = CampoImportacao.Nome,
        ["nome completo"] = CampoImportacao.Nome,
        ["full name"] = CampoImportacao.Nome,
        ["fullname"] = CampoImportacao.Nome,
        ["name"] = CampoImportacao.Nome,
        ["cliente"] = CampoImportacao.Nome,
        ["nome do cliente"] = CampoImportacao.Nome,
        ["contato"] = CampoImportacao.Nome,
        ["nome do contato"] = CampoImportacao.Nome,
        ["nome e sobrenome"] = CampoImportacao.Nome,
        ["email"] = CampoImportacao.Email,
        ["e mail"] = CampoImportacao.Email,
        ["mail"] = CampoImportacao.Email,
        ["email address"] = CampoImportacao.Email,
        ["endereco de email"] = CampoImportacao.Email,
        ["endereco de e mail"] = CampoImportacao.Email,
        ["correio eletronico"] = CampoImportacao.Email,
        ["telefone"] = CampoImportacao.Telefone,
        ["phone"] = CampoImportacao.Telefone,
        ["phone number"] = CampoImportacao.Telefone,
        ["mobile"] = CampoImportacao.Telefone,
        ["mobile phone"] = CampoImportacao.Telefone,
        ["celular"] = CampoImportacao.Telefone,
        ["whatsapp"] = CampoImportacao.Telefone,
        ["fone"] = CampoImportacao.Telefone,
        ["tel"] = CampoImportacao.Telefone,
        ["telefone celular"] = CampoImportacao.Telefone,
        ["telefone whatsapp"] = CampoImportacao.Telefone,
        ["numero de telefone"] = CampoImportacao.Telefone,
        ["numero do whatsapp"] = CampoImportacao.Telefone,
        ["cpf"] = CampoImportacao.CpfCnpj,
        ["cnpj"] = CampoImportacao.CpfCnpj,
        ["cpf cnpj"] = CampoImportacao.CpfCnpj,
        ["cnpj cpf"] = CampoImportacao.CpfCnpj,
        ["cpf ou cnpj"] = CampoImportacao.CpfCnpj,
        ["documento"] = CampoImportacao.CpfCnpj,
        ["uf"] = CampoImportacao.Uf,
        ["estado"] = CampoImportacao.Uf,
        ["state"] = CampoImportacao.Uf,
        ["province"] = CampoImportacao.Uf,
        ["region"] = CampoImportacao.Uf,
        ["sigla do estado"] = CampoImportacao.Uf,
        ["empresa"] = CampoImportacao.Empresa,
        ["company"] = CampoImportacao.Empresa,
        ["company name"] = CampoImportacao.Empresa,
        ["nome da empresa"] = CampoImportacao.Empresa,
        ["razao social"] = CampoImportacao.Empresa,
        ["nome fantasia"] = CampoImportacao.Empresa,
        ["organizacao"] = CampoImportacao.Empresa,
        ["organization"] = CampoImportacao.Empresa,
        ["campaign id"] = CampoImportacao.CampanhaId,
        ["campanha id"] = CampoImportacao.CampanhaId,
        ["id da campanha"] = CampoImportacao.CampanhaId,
        ["campaign name"] = CampoImportacao.CampanhaNome,
        ["campaign"] = CampoImportacao.CampanhaNome,
        ["campanha"] = CampoImportacao.CampanhaNome,
        ["nome da campanha"] = CampoImportacao.CampanhaNome,
        ["adset id"] = CampoImportacao.ConjuntoId,
        ["ad set id"] = CampoImportacao.ConjuntoId,
        ["ad group id"] = CampoImportacao.ConjuntoId,
        ["adgroup id"] = CampoImportacao.ConjuntoId,
        ["id do conjunto"] = CampoImportacao.ConjuntoId,
        ["adset name"] = CampoImportacao.ConjuntoNome,
        ["ad set name"] = CampoImportacao.ConjuntoNome,
        ["ad group name"] = CampoImportacao.ConjuntoNome,
        ["adgroup name"] = CampoImportacao.ConjuntoNome,
        ["conjunto"] = CampoImportacao.ConjuntoNome,
        ["conjunto de anuncios"] = CampoImportacao.ConjuntoNome,
        ["grupo de anuncios"] = CampoImportacao.ConjuntoNome,
        ["ad id"] = CampoImportacao.AnuncioId,
        ["id do anuncio"] = CampoImportacao.AnuncioId,
        ["ad name"] = CampoImportacao.AnuncioNome,
        ["anuncio"] = CampoImportacao.AnuncioNome,
        ["nome do anuncio"] = CampoImportacao.AnuncioNome,
        ["form id"] = CampoImportacao.FormularioId,
        ["id do formulario"] = CampoImportacao.FormularioId,
        ["form name"] = CampoImportacao.FormularioNome,
        ["formulario"] = CampoImportacao.FormularioNome,
        ["nome do formulario"] = CampoImportacao.FormularioNome,
        ["platform"] = CampoImportacao.Plataforma,
        ["plataforma"] = CampoImportacao.Plataforma,
        ["utm source"] = CampoImportacao.UtmSource,
        ["utm medium"] = CampoImportacao.UtmMedium,
        ["utm campaign"] = CampoImportacao.UtmCampaign,
        ["utm content"] = CampoImportacao.UtmContent,
        ["utm term"] = CampoImportacao.UtmTerm,
        ["gclid"] = CampoImportacao.Gclid,
        ["gcl id"] = CampoImportacao.Gclid,
    }.ToFrozenDictionary();

    /// <summary>Partes do nome: juntadas no nome, a não ser que o arquivo tenha uma coluna com o nome completo.</summary>
    private static readonly FrozenSet<string> PartesDoNome =
        new[] { "first name", "last name", "primeiro nome", "sobrenome", "ultimo nome" }.ToFrozenSet();

    /// <summary>Colunas que só podem ser o id do lead na origem.</summary>
    private static readonly FrozenSet<string> ColunasDeId = new[] { "lead id", "leadgen id" }.ToFrozenSet();

    /// <summary>Colunas da exportação da Meta que não vão para o lead (o id vai como id externo).</summary>
    private static readonly FrozenSet<string> ColunasTecnicas =
        new[] { "id", "lead id", "leadgen id", "created time", "is organic", "lead status", "inbox url" }.ToFrozenSet();

    public static SugestaoImportacao Sugerir(TabelaCsv tabela)
    {
        var nomes = tabela.Colunas.Select(Simplificar).ToList();
        var temNomeCompleto = nomes.Any(n => CamposPorNome.GetValueOrDefault(n) == CampoImportacao.Nome);
        var campos = new List<CampoImportacao>(nomes.Count);
        foreach (var nome in nomes)
        {
            var campo = PartesDoNome.Contains(nome) ? (temNomeCompleto ? CampoImportacao.NaoImportar : CampoImportacao.Nome)
                : ColunasTecnicas.Contains(nome) ? CampoImportacao.NaoImportar
                : CamposPorNome.GetValueOrDefault(nome, CampoImportacao.RespostaExtra);

            // Um campo de uma coluna só fica com a primeira; uma segunda (um "Celular" depois do "Telefone") não se perde.
            campos.Add(CamposImportacao.UmaColunaSo(campo) && campos.Contains(campo) ? CampoImportacao.RespostaExtra : campo);
        }

        var colunaId = ColunaDoIdExterno(nomes, tabela);
        return new SugestaoImportacao(campos, colunaId, DaMeta(nomes, tabela, colunaId) ? Origem.MetaLeadAds : Origem.Planilha);
    }

    /// <summary>
    /// Uma coluna "lead_id", ou uma coluna "id" com os ids de lead da Meta (<c>l:123</c>). Um "id" qualquer não é
    /// sugerido: o número da linha de uma planilha se repete entre arquivos e juntaria pessoas diferentes.
    /// </summary>
    private static int? ColunaDoIdExterno(List<string> nomes, TabelaCsv tabela)
    {
        for (var i = 0; i < nomes.Count; i++)
        {
            if (ColunasDeId.Contains(nomes[i]) || (nomes[i] == "id" && TemIdDeLeadDaMeta(tabela, i)))
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>O arquivo exportado pela Meta: tem o formulário e o anúncio ou a plataforma, ou os ids de lead dela.</summary>
    private static bool DaMeta(List<string> nomes, TabelaCsv tabela, int? colunaId) =>
        (nomes.Contains("form id") && (nomes.Contains("platform") || nomes.Contains("is organic") || nomes.Contains("ad id")))
        || (colunaId is { } coluna && TemIdDeLeadDaMeta(tabela, coluna));

    private static bool TemIdDeLeadDaMeta(TabelaCsv tabela, int coluna) =>
        tabela.Linhas.Take(LinhasConferidas).Any(l => PrefixosMeta.EIdDeLead(l.Valores[coluna]));

    /// <summary>Sem acentos, em minúsculas, com letras e números separados por um espaço: "E-mail" vira "e mail".</summary>
    internal static string Simplificar(string nome)
    {
        var simplificado = new StringBuilder(nome.Length);
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            simplificado.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', simplificado.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
