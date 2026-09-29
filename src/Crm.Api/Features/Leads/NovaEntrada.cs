using System.Text.Json;
using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.Api.Features.Leads;

/// <summary>Campanha, conjunto, anúncio ou formulário da plataforma de anúncios. O Google manda só o id da campanha.</summary>
public sealed record ReferenciaExterna(string? Id = null, string? Nome = null);

public sealed record Utm(
    string? Source = null, string? Medium = null, string? Campaign = null, string? Content = null, string? Term = null);

/// <summary>
/// Um lead chegando, por qualquer caminho (integração, cadastro manual ou importação), com os dados como vieram.
/// A normalização acontece no <see cref="ReceberEntrada"/>.
/// </summary>
public sealed record NovaEntrada
{
    public required Origem Origem { get; init; }

    /// <summary>Id na origem; com ele, a entrada é idempotente. Nulo no cadastro manual.</summary>
    public string? IdExterno { get; init; }

    public required DateTimeOffset RecebidoEm { get; init; }

    public bool Teste { get; init; }

    public required string Nome { get; init; }

    public string? Email { get; init; }

    public string? Telefone { get; init; }

    public string? CpfCnpj { get; init; }

    public string? Uf { get; init; }

    public string? Empresa { get; init; }

    public string? Plataforma { get; init; }

    public ReferenciaExterna? Campanha { get; init; }

    public ReferenciaExterna? Conjunto { get; init; }

    public ReferenciaExterna? Anuncio { get; init; }

    public ReferenciaExterna? Formulario { get; init; }

    public Utm? Utm { get; init; }

    public string? Gclid { get; init; }

    public IReadOnlyDictionary<string, JsonElement>? RespostasExtras { get; init; }

    public JsonDocument? PayloadBruto { get; init; }

    /// <summary>
    /// Responsável de um lead novo, no cadastro manual. Nulo deixa para a distribuição automática (seção 3.3,
    /// Passo 4); até lá, o lead fica sem responsável. Lead de teste nunca tem responsável.
    /// </summary>
    public Guid? ResponsavelId { get; init; }

    /// <summary>Quem registrou a entrada, no cadastro manual. Nulo na integração.</summary>
    public Guid? UsuarioId { get; init; }
}

/// <param name="LeadId">O lead criado, o lead ao qual a entrada foi vinculada ou o da entrada já recebida.</param>
public sealed record EntradaProcessada(long LeadId, ResultadoEntrada Resultado);

/// <summary>Dados pessoais da entrada normalizados e cortados no tamanho das colunas.</summary>
internal sealed record DadosPessoa(string Nome, string? Email, string? Telefone, string? CpfCnpj, string? Uf, string? Empresa)
{
    public static DadosPessoa De(NovaEntrada entrada) => new(
        Textos.LimparECortar(entrada.Nome, Lead.TamanhoMaximoNome)
            ?? throw new ArgumentException("A entrada precisa de um nome.", nameof(entrada)),
        Textos.Cortar(Emails.Normalizar(entrada.Email), Emails.TamanhoMaximo),
        Textos.Cortar(Telefones.Normalizar(entrada.Telefone), Lead.TamanhoMaximoTelefone),
        Textos.Cortar(Documentos.Normalizar(entrada.CpfCnpj), Lead.TamanhoMaximoDocumento),
        Ufs.Normalizar(entrada.Uf),
        Textos.LimparECortar(entrada.Empresa, Lead.TamanhoMaximoEmpresa));

    public bool DocumentoValido => Documentos.Valido(CpfCnpj);

    public bool EmailValido => Emails.Valido(Email);

    public bool TelefoneValido => Telefones.Valido(Telefone);
}
