namespace Crm.Api.Infra.TempoReal;

/// <summary>Uma troca de responsável: quem tinha o lead e o perdeu recebe um aviso sem ids.</summary>
public sealed record TrocaDeResponsavel(long LeadId, Guid AnteriorId);

/// <summary>Uma notificação gravada, para o aviso ao dono dela.</summary>
public sealed record NotificacaoGravada(long Id, Guid UsuarioId);

/// <summary>
/// O que um commit gravou e interessa às telas: os leads que mudaram (criados, editados, com uma interação, entrada ou
/// mudança de etapa nova), as trocas de responsável, as notificações novas e se as etapas ou os motivos de perda mudaram.
/// </summary>
public sealed record MudancasTempoReal(
    IReadOnlyCollection<long> Leads,
    IReadOnlyCollection<TrocaDeResponsavel> Trocas,
    IReadOnlyCollection<NotificacaoGravada> Notificacoes,
    bool Funil)
{
    /// <summary>Junta as mudanças de vários <c>SaveChanges</c> da mesma transação num aviso só.</summary>
    public static MudancasTempoReal Juntar(IReadOnlyCollection<MudancasTempoReal> partes) => new(
        [.. partes.SelectMany(p => p.Leads).Distinct()],
        [.. partes.SelectMany(p => p.Trocas).Distinct()],
        [.. partes.SelectMany(p => p.Notificacoes).Distinct()],
        partes.Any(p => p.Funil));
}

/// <summary>A situação de um lead na hora do envio.</summary>
internal sealed record LeadNoEnvio(Guid? ResponsavelId, bool Teste);

/// <summary>Para quem vai cada aviso de um lote de mudanças.</summary>
/// <param name="LeadsParaAdmins">Todos os leads: os admins veem todos, inclusive os de teste.</param>
/// <param name="LeadsPorUsuario">Os leads de cada responsável, sem os de teste.</param>
/// <param name="RecarregarTudo">Quem perdeu um lead numa troca: recebe um aviso sem ids.</param>
internal sealed record EnvioTempoReal(
    long[] LeadsParaAdmins,
    IReadOnlyDictionary<Guid, long[]> LeadsPorUsuario,
    IReadOnlyCollection<Guid> RecarregarTudo,
    IReadOnlyCollection<NotificacaoGravada> Notificacoes,
    bool Funil)
{
    /// <summary>
    /// Quem recebe o quê. A atendente só recebe o id de um lead que é dela agora (a responsável é lida no banco na hora
    /// do envio, em <paramref name="leads"/>). Quem tinha o lead e o perdeu numa troca recebe um aviso sem ids, que
    /// recarrega o que estiver aberto: o id de um lead que já é de outra pessoa não chega até ela. Um lead que não está
    /// em <paramref name="leads"/> (excluído) só vai para os admins.
    /// </summary>
    public static EnvioTempoReal Montar(IReadOnlyCollection<MudancasTempoReal> lote, IReadOnlyDictionary<long, LeadNoEnvio> leads)
    {
        var ids = lote.SelectMany(m => m.Leads.Concat(m.Trocas.Select(t => t.LeadId))).Distinct().Order().ToArray();
        var porUsuario = ids
            .Select(id => (Id: id, Lead: leads.GetValueOrDefault(id)))
            .Where(l => l.Lead is { Teste: false, ResponsavelId: not null })
            .GroupBy(l => l.Lead!.ResponsavelId!.Value, l => l.Id)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var recarregar = lote
            .SelectMany(m => m.Trocas)
            .Where(t => leads.GetValueOrDefault(t.LeadId)?.ResponsavelId != t.AnteriorId)
            .Select(t => t.AnteriorId)
            .Distinct()
            .ToList();
        return new EnvioTempoReal(ids, porUsuario, recarregar, [.. lote.SelectMany(m => m.Notificacoes)], lote.Any(m => m.Funil));
    }
}
