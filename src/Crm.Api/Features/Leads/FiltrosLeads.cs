using Crm.Api.Infra.Datas;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Leads;

/// <summary>Filtros que a lista de leads e o quadro do funil têm em comum.</summary>
public interface IFiltroLeads
{
    /// <summary>Nome, empresa ou e-mail, sem acento; os dígitos também procuram no telefone e no CPF/CNPJ.</summary>
    string? Busca { get; }

    Origem? Origem { get; }

    int? CampanhaId { get; }

    /// <summary>Só para o admin.</summary>
    Guid? ResponsavelId { get; }

    /// <summary>Só para o admin: leads que ainda não têm responsável.</summary>
    bool SemResponsavel { get; }
}

internal static class FiltrosLeads
{
    /// <summary>
    /// Aplica os filtros comuns. A consulta já vem de <see cref="VisibilidadeLeads"/>: os filtros de responsável
    /// são do admin, e a atendente continua vendo só os próprios leads.
    /// </summary>
    public static IQueryable<Lead> Aplicar(IQueryable<Lead> consulta, IFiltroLeads filtro, bool ehAdmin)
    {
        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            consulta = Buscar(consulta, filtro.Busca);
        }

        if (filtro.Origem is { } origem)
        {
            consulta = consulta.Where(l => l.Origem == origem);
        }

        if (filtro.CampanhaId is { } campanhaId)
        {
            consulta = consulta.Where(l => l.CampanhaId == campanhaId);
        }

        if (ehAdmin && filtro.SemResponsavel)
        {
            consulta = consulta.Where(l => l.ResponsavelId == null);
        }
        else if (ehAdmin && filtro.ResponsavelId is { } responsavelId)
        {
            consulta = consulta.Where(l => l.ResponsavelId == responsavelId);
        }

        return consulta;
    }

    /// <summary>
    /// Os filtros da lista de leads, os mesmos na exportação: os comuns, a etapa e o período de entrada, em dias no horário
    /// de Brasília.
    /// </summary>
    public static IQueryable<Lead> DaLista(IQueryable<Lead> consulta, FiltroLeads filtro, bool ehAdmin)
    {
        consulta = Aplicar(consulta, filtro, ehAdmin);

        if (filtro.EtapaId is { } etapaId)
        {
            consulta = consulta.Where(l => l.EtapaId == etapaId);
        }

        if (filtro.De is { } de)
        {
            var inicio = HorarioBrasilia.InicioDoDia(de);
            consulta = consulta.Where(l => l.CriadoEm >= inicio);
        }

        if (filtro.Ate is { } ate)
        {
            var fim = HorarioBrasilia.InicioDoDia(ate.AddDays(1));
            consulta = consulta.Where(l => l.CriadoEm < fim);
        }

        return consulta;
    }

    /// <summary>A ordem escolhida na lista, com o id para desempatar.</summary>
    public static IOrderedQueryable<Lead> Ordenar(IQueryable<Lead> consulta, OrdemLeads ordem) => ordem switch
    {
        OrdemLeads.Antigos => consulta.OrderBy(l => l.CriadoEm).ThenBy(l => l.Id),
        OrdemLeads.Nome => consulta.OrderBy(l => l.Nome).ThenBy(l => l.Id),
        _ => consulta.OrderByDescending(l => l.CriadoEm).ThenByDescending(l => l.Id),
    };

    /// <summary>Nome e empresa sem acento, e-mail, e os dígitos no telefone e no CPF/CNPJ.</summary>
    private static IQueryable<Lead> Buscar(IQueryable<Lead> consulta, string busca)
    {
        const int MinimoDeCaracteresNoDocumento = 4;
        var termo = busca.Trim();
        var padrao = $"%{EscaparLike(termo)}%";
        var digitos = string.Concat(termo.Where(char.IsAsciiDigit));
        var documento = string.Concat(termo.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        var buscaTelefone = digitos.Length >= MinimoDeCaracteresNoDocumento;
        var buscaDocumento = documento.Length >= MinimoDeCaracteresNoDocumento;

        return consulta.Where(l =>
            EF.Functions.ILike(EF.Functions.Unaccent(l.Nome), EF.Functions.Unaccent(padrao))
            || (l.Empresa != null && EF.Functions.ILike(EF.Functions.Unaccent(l.Empresa), EF.Functions.Unaccent(padrao)))
            || (l.Email != null && EF.Functions.ILike(l.Email, padrao))
            || (buscaTelefone && l.Telefone != null && l.Telefone.Contains(digitos))
            || (buscaDocumento && l.CpfCnpj != null && l.CpfCnpj.Contains(documento)));
    }

    /// <summary>A barra invertida é o escape padrão do LIKE no PostgreSQL.</summary>
    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
