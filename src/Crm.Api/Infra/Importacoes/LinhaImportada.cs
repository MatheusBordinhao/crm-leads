using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.Api.Infra.Importacoes;

/// <summary>O que vale para todas as linhas de uma importação.</summary>
/// <param name="RecebidoEm">A hora da importação, a mesma em todas as linhas, inclusive quando ela é retomada.</param>
/// <param name="Colunas">Os nomes das colunas do arquivo.</param>
/// <param name="Campos">O campo de cada coluna, na mesma ordem.</param>
/// <param name="CampanhaPadrao">A campanha das linhas que não trazem a sua.</param>
/// <param name="ResponsavelId">Quem recebe os leads novos. Nulo: o rodízio.</param>
public sealed record ContextoImportacao(
    long ImportacaoId,
    Guid UsuarioId,
    DateTimeOffset RecebidoEm,
    Origem Origem,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<CampoImportacao> Campos,
    int? ColunaIdExterno,
    ReferenciaExterna? CampanhaPadrao,
    Guid? ResponsavelId);

/// <summary>
/// A entrada que uma linha do arquivo gera, ou o motivo de ela não entrar. Só a falta de nome e de contato recusa a
/// linha: um e-mail, telefone ou documento em formato inválido entra com o aviso na ficha, como na integração.
/// </summary>
public sealed record LinhaImportada(NovaEntrada? Entrada, string? Erro)
{
    public const string ErroSemNome = "A linha não tem o nome.";

    public const string ErroSemContato = "A linha não tem e-mail nem telefone.";

    public static readonly string ErroIdLongo = $"O id externo passa de {Campanha.TamanhoMaximoIdExterno} caracteres.";

    public static LinhaImportada Montar(LinhaCsv linha, ContextoImportacao contexto)
    {
        if (linha.Erro is not null)
        {
            return Recusada(linha.Erro);
        }

        string? Valor(CampoImportacao campo)
        {
            var coluna = IndiceDe(contexto.Campos, campo);
            return coluna < 0 ? null : Preenchido(linha.Valores[coluna]);
        }

        // O nome junta as colunas marcadas como nome, na ordem do arquivo: "first_name" e "last_name".
        var nome = Textos.Limpar(string.Join(' ', contexto.Campos
            .Select((campo, coluna) => (campo, coluna))
            .Where(c => c.campo == CampoImportacao.Nome)
            .Select(c => linha.Valores[c.coluna])));
        if (nome is null)
        {
            return Recusada(ErroSemNome);
        }

        var email = Valor(CampoImportacao.Email);
        var telefone = PrefixosMeta.Telefone(Valor(CampoImportacao.Telefone));
        if (email is null && telefone is null)
        {
            return Recusada(ErroSemContato);
        }

        var idDaColuna = contexto.ColunaIdExterno is { } colunaId ? PrefixosMeta.Id(Preenchido(linha.Valores[colunaId])) : null;
        if (idDaColuna?.Length > Campanha.TamanhoMaximoIdExterno)
        {
            return Recusada(ErroIdLongo);
        }

        var utm = new Utm(
            Valor(CampoImportacao.UtmSource),
            Valor(CampoImportacao.UtmMedium),
            Valor(CampoImportacao.UtmCampaign),
            Valor(CampoImportacao.UtmContent),
            Valor(CampoImportacao.UtmTerm));
        var respostas = RespostasExtras(linha, contexto);

        return new LinhaImportada(
            new NovaEntrada
            {
                Origem = contexto.Origem,
                IdExterno = idDaColuna ?? IdDoConteudo(linha, contexto.Colunas),
                RecebidoEm = contexto.RecebidoEm,
                Nome = nome,
                Email = email,
                Telefone = telefone,
                CpfCnpj = Valor(CampoImportacao.CpfCnpj),
                Uf = Valor(CampoImportacao.Uf),
                Empresa = Valor(CampoImportacao.Empresa),
                Plataforma = Plataforma(Valor(CampoImportacao.Plataforma)),
                Campanha = Referencia(Valor(CampoImportacao.CampanhaId), Valor(CampoImportacao.CampanhaNome), nomeServeDeId: true)
                    ?? contexto.CampanhaPadrao,
                Conjunto = Referencia(Valor(CampoImportacao.ConjuntoId), Valor(CampoImportacao.ConjuntoNome)),
                Anuncio = Referencia(Valor(CampoImportacao.AnuncioId), Valor(CampoImportacao.AnuncioNome)),
                Formulario = Referencia(Valor(CampoImportacao.FormularioId), Valor(CampoImportacao.FormularioNome)),
                Utm = utm == new Utm() ? null : utm,
                Gclid = Valor(CampoImportacao.Gclid),
                RespostasExtras = respostas.Count > 0 ? respostas : null,
                PayloadBruto = JsonSerializer.SerializeToDocument(new
                {
                    linha = linha.Numero,
                    valores = contexto.Colunas.Zip(linha.Valores).ToDictionary(c => c.First, c => c.Second),
                }),
                ResponsavelId = contexto.ResponsavelId,
                UsuarioId = contexto.UsuarioId,
                ImportacaoId = contexto.ImportacaoId,
                Avisar = false,
            },
            null);
    }

    private static LinhaImportada Recusada(string motivo) => new(null, motivo);

    private static int IndiceDe(IReadOnlyList<CampoImportacao> campos, CampoImportacao campo)
    {
        for (var i = 0; i < campos.Count; i++)
        {
            if (campos[i] == campo)
            {
                return i;
            }
        }

        return -1;
    }

    private static string? Preenchido(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>
    /// O id externo de uma linha sem id: o SHA-256 das colunas preenchidas, com o nome de cada uma, em ordem
    /// alfabética. A mesma linha dá sempre o mesmo id, mesmo com as colunas em outra ordem, e reimportar o arquivo
    /// responde "já importado".
    /// </summary>
    private static string IdDoConteudo(LinhaCsv linha, IReadOnlyList<string> colunas)
    {
        var preenchidas = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < colunas.Count; i++)
        {
            if (Preenchido(linha.Valores[i]) is { } valor)
            {
                preenchidas[colunas[i]] = valor;
            }
        }

        return $"sha256:{Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(preenchidas)))}";
    }

    /// <summary>Campanha, conjunto, anúncio ou formulário. Uma campanha só com o nome usa o nome como id.</summary>
    private static ReferenciaExterna? Referencia(string? id, string? nome, bool nomeServeDeId = false)
    {
        id = PrefixosMeta.Id(id);
        if (id is null && nomeServeDeId)
        {
            id = Textos.Cortar(nome, Campanha.TamanhoMaximoIdExterno);
        }

        return id is null && nome is null ? null : new ReferenciaExterna(id, nome);
    }

    /// <summary>A Meta exporta a plataforma como <c>fb</c> e <c>ig</c>; a integração usa o nome inteiro.</summary>
    private static string? Plataforma(string? valor) => valor?.ToLowerInvariant() switch
    {
        "fb" => "facebook",
        "ig" => "instagram",
        _ => valor,
    };

    private static Dictionary<string, JsonElement> RespostasExtras(LinhaCsv linha, ContextoImportacao contexto)
    {
        var respostas = new Dictionary<string, JsonElement>();
        for (var i = 0; i < contexto.Campos.Count; i++)
        {
            if (contexto.Campos[i] == CampoImportacao.RespostaExtra && Preenchido(linha.Valores[i]) is { } resposta)
            {
                respostas[contexto.Colunas[i]] = JsonSerializer.SerializeToElement(resposta);
            }
        }

        return respostas;
    }
}

/// <summary>
/// Prefixos que a exportação de leads da Meta costuma pôr nos ids e no telefone, para a planilha não estragar números
/// longos: <c>l:</c> no lead, <c>c:</c> na campanha, <c>as:</c> no conjunto, <c>ag:</c> no anúncio, <c>f:</c> no
/// formulário e <c>p:</c> no telefone. Sem eles, os ids batem com os que a integração manda. O prefixo só sai quando o
/// valor tem exatamente esse formato.
/// </summary>
internal static partial class PrefixosMeta
{
    public static bool EIdDeLead(string valor) => IdComPrefixo().Match(valor.Trim()) is { Success: true } m && m.Groups["prefixo"].Value is "l" or "L";

    public static string? Id(string? valor) =>
        valor is not null && IdComPrefixo().Match(valor) is { Success: true } m ? m.Groups["id"].Value : valor;

    public static string? Telefone(string? valor) =>
        valor is not null && TelefoneComPrefixo().Match(valor) is { Success: true } m ? m.Groups["telefone"].Value : valor;

    [GeneratedRegex(@"^(?<prefixo>l|c|as|ag|f):(?<id>\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex IdComPrefixo();

    [GeneratedRegex(@"^p:(?<telefone>\+?[\d\s().-]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex TelefoneComPrefixo();
}
