using System.Text.Json;
using Crm.Api.Features.Leads;
using Crm.Api.Infra.Datas;
using Crm.Api.Infra.Web;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Infra.Auditoria;

/// <summary>Filtros da consulta da auditoria, na query string. Todos são opcionais.</summary>
public sealed class FiltroAuditoria
{
    public const int TamanhoPadrao = 50;

    public const int TamanhoMaximo = 100;

    /// <summary>Primeiro dia, no horário de Brasília.</summary>
    [FromQuery(Name = "de")]
    public DateOnly? De { get; set; }

    /// <summary>Último dia, no horário de Brasília.</summary>
    [FromQuery(Name = "ate")]
    public DateOnly? Ate { get; set; }

    /// <summary>Quem fez a ação.</summary>
    [FromQuery(Name = "usuarioId")]
    public Guid? UsuarioId { get; set; }

    /// <summary>A ação, como <c>lead.anonimizado</c>.</summary>
    [FromQuery(Name = "acao")]
    public string? Acao { get; set; }

    /// <summary>O tipo do registro afetado: <c>lead</c>, <c>usuario</c>, <c>etapa</c>, <c>motivo_perda</c>, <c>configuracao</c> ou <c>importacao</c>.</summary>
    [FromQuery(Name = "entidade")]
    public string? Entidade { get; set; }

    /// <summary>O id do registro afetado, como o id do lead.</summary>
    [FromQuery(Name = "entidadeId")]
    public string? EntidadeId { get; set; }

    [FromQuery(Name = "pagina")]
    public int Pagina { get; set; } = 1;

    [FromQuery(Name = "tamanho")]
    public int Tamanho { get; set; } = TamanhoPadrao;
}

internal sealed class FiltroAuditoriaValidator : Validador<FiltroAuditoria>
{
    public FiltroAuditoriaValidator()
    {
        RuleFor(f => f.Pagina).GreaterThanOrEqualTo(1).WithMessage("A página começa em 1.");
        RuleFor(f => f.Tamanho)
            .InclusiveBetween(1, FiltroAuditoria.TamanhoMaximo)
            .WithMessage($"O tamanho da página vai de 1 a {FiltroAuditoria.TamanhoMaximo}.");
        RuleFor(f => f.De).Must(HorarioBrasilia.DataAceita).WithMessage(HorarioBrasilia.MensagemData);
        RuleFor(f => f.Ate).Must(HorarioBrasilia.DataAceita).WithMessage(HorarioBrasilia.MensagemData);
        RuleFor(f => f.Ate)
            .GreaterThanOrEqualTo(f => f.De)
            .When(f => f.De is not null && f.Ate is not null)
            .WithMessage("O fim do período vem depois do início.");
    }
}

/// <summary>Um registro da auditoria (seção 3.8), sem dados pessoais.</summary>
/// <param name="Usuario">Quem fez. Nulo no login com e-mail desconhecido e no que o sistema fez sozinho.</param>
/// <param name="Acao">O que foi feito, como <c>login.sucesso</c> ou <c>lead.anonimizado</c>.</param>
/// <param name="EntidadeId">O id do registro afetado, como o id do lead ou do usuário.</param>
/// <param name="Detalhes">O que mudou, como os nomes dos campos editados ou os filtros de uma exportação, sem os valores.</param>
/// <param name="Ip">O IP de quem acessou. Nulo no que o sistema fez fora de uma requisição.</param>
public sealed record RegistroAuditoriaResponse(
    long Id,
    DateTimeOffset OcorridoEm,
    UsuarioResumo? Usuario,
    string Acao,
    string? Entidade,
    string? EntidadeId,
    JsonElement? Detalhes,
    string? Ip);

/// <param name="Total">Quantos registros passam nos filtros, somando todas as páginas.</param>
public sealed record ListaAuditoriaResponse(IReadOnlyList<RegistroAuditoriaResponse> Itens, int Total, int Pagina, int Tamanho);
