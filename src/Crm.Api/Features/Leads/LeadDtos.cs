using Crm.Api.Features.Funil;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Features.Leads;

public sealed record EtapaResumo(int Id, string Nome, TipoEtapa Tipo);

public sealed record ResponsavelResumo(Guid Id, string Nome);

public sealed record CampanhaResumo(int Id, string Nome);

public sealed record LeadResumoResponse(
    long Id,
    string Nome,
    string? Empresa,
    string? Email,
    string? Telefone,
    EtapaResumo Etapa,
    Origem Origem,
    CampanhaResumo? Campanha,
    ResponsavelResumo? Responsavel,
    bool Teste,
    DateTimeOffset CriadoEm);

/// <param name="Total">Quantos leads passam nos filtros, somando todas as páginas.</param>
public sealed record ListaLeadsResponse(IReadOnlyList<LeadResumoResponse> Itens, int Total, int Pagina, int Tamanho);

public sealed record RespostaExtra(string Pergunta, string Resposta);

/// <summary>Ficha do lead.</summary>
/// <param name="Avisos">Dados que precisam de atenção (seção 3.1), com o campo a que se referem.</param>
/// <param name="Versao">Vai de volta na edição: se o lead mudou nesse meio-tempo, a edição responde 409.</param>
public sealed record LeadResponse(
    long Id,
    string Nome,
    string? Email,
    string? Telefone,
    string? CpfCnpj,
    string? Uf,
    string? Empresa,
    EtapaResumo Etapa,
    ResponsavelResumo? Responsavel,
    Origem Origem,
    string? Plataforma,
    CampanhaResumo? Campanha,
    string? UtmSource,
    string? UtmMedium,
    string? UtmCampaign,
    string? UtmContent,
    string? UtmTerm,
    string? Gclid,
    IReadOnlyList<RespostaExtra> RespostasExtras,
    bool Teste,
    DateTimeOffset CriadoEm,
    DateTimeOffset AtualizadoEm,
    DateTimeOffset? PrimeiroContatoEm,
    DateTimeOffset? ConvertidoEm,
    DateTimeOffset? PerdidoEm,
    DateTimeOffset? AnonimizadoEm,
    IReadOnlyList<AvisoLead> Avisos,
    uint Versao);

/// <summary>Dados que a tela edita, iguais no cadastro e na edição.</summary>
public interface IDadosLead
{
    string Nome { get; }

    string? Email { get; }

    string? Telefone { get; }

    string? CpfCnpj { get; }

    string? Uf { get; }

    string? Empresa { get; }
}

/// <param name="Origem"><c>manual</c>, <c>telefone</c>, <c>whatsapp</c> ou <c>indicacao</c>.</param>
/// <param name="ResponsavelId">
/// Só o admin escolhe. Nulo: distribuição automática, que chega no Passo 4 (até lá, o lead fica sem
/// responsável). A atendente fica com o lead que cadastra.
/// </param>
public sealed record CriarLeadRequest(
    string Nome,
    string? Email,
    string? Telefone,
    string? CpfCnpj,
    string? Uf,
    string? Empresa,
    Origem Origem,
    Guid? ResponsavelId) : IDadosLead;

/// <param name="LeadId">
/// Nulo quando a pessoa já é lead de outra atendente: os dados vão para a ficha dela, mas a resposta não diz
/// qual lead é nem de quem.
/// </param>
/// <param name="Situacao"><c>criado</c> (201) ou <c>vinculado</c> (200), quando a pessoa já era um lead.</param>
/// <param name="PodeAbrir">Falso quando a pessoa já é lead de outra atendente.</param>
public sealed record CriarLeadResponse(long? LeadId, ResultadoEntrada Situacao, bool PodeAbrir);

/// <param name="Versao">A <c>versao</c> da ficha que a pessoa editou.</param>
public sealed record EditarLeadRequest(
    string Nome,
    string? Email,
    string? Telefone,
    string? CpfCnpj,
    string? Uf,
    string? Empresa,
    uint Versao) : IDadosLead;

public enum OrdemLeads
{
    Recentes = 1,
    Antigos = 2,
    Nome = 3,
}

/// <summary>Filtros da lista de leads, na query string, com os nomes em camelCase como no resto da API.</summary>
public sealed class FiltroLeads
{
    public const int TamanhoPadrao = 25;

    public const int TamanhoMaximo = 100;

    /// <summary>Nome, empresa ou e-mail, sem acento; os dígitos também procuram no telefone e no CPF/CNPJ.</summary>
    [FromQuery(Name = "busca")]
    public string? Busca { get; set; }

    [FromQuery(Name = "etapaId")]
    public int? EtapaId { get; set; }

    [FromQuery(Name = "origem")]
    public Origem? Origem { get; set; }

    [FromQuery(Name = "campanhaId")]
    public int? CampanhaId { get; set; }

    /// <summary>Só para o admin.</summary>
    [FromQuery(Name = "responsavelId")]
    public Guid? ResponsavelId { get; set; }

    /// <summary>Só para o admin: leads que ainda não têm responsável.</summary>
    [FromQuery(Name = "semResponsavel")]
    public bool SemResponsavel { get; set; }

    /// <summary>Primeiro dia do período de entrada, no horário de Brasília.</summary>
    [FromQuery(Name = "de")]
    public DateOnly? De { get; set; }

    /// <summary>Último dia do período de entrada, no horário de Brasília.</summary>
    [FromQuery(Name = "ate")]
    public DateOnly? Ate { get; set; }

    [FromQuery(Name = "ordem")]
    public OrdemLeads Ordem { get; set; } = OrdemLeads.Recentes;

    [FromQuery(Name = "pagina")]
    public int Pagina { get; set; } = 1;

    [FromQuery(Name = "tamanho")]
    public int Tamanho { get; set; } = TamanhoPadrao;
}
