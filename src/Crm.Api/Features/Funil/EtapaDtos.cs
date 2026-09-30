using Crm.Api.Infra.Web;
using FluentValidation;

namespace Crm.Api.Features.Funil;

public sealed record EtapaResponse(int Id, string Nome, int Ordem, TipoEtapa Tipo, bool Ativa);

public sealed record CriarEtapaRequest(string Nome) : ICadastroComNome
{
    public override string ToString() => nameof(CriarEtapaRequest);
}

public sealed record RenomearEtapaRequest(string Nome) : ICadastroComNome
{
    public override string ToString() => nameof(RenomearEtapaRequest);
}

/// <param name="Ids">Todas as etapas abertas (ativas ou não), na ordem nova. Ganho e Perdido ficam sempre no fim.</param>
public sealed record ReordenarEtapasRequest(IReadOnlyList<int> Ids)
{
    public override string ToString() => nameof(ReordenarEtapasRequest);
}

/// <param name="MoverLeadsParaId">
/// Etapa aberta ativa que recebe os leads da etapa desativada. Obrigatória só quando a etapa tem leads.
/// </param>
public sealed record DesativarEtapaRequest(int? MoverLeadsParaId)
{
    public override string ToString() => $"{nameof(DesativarEtapaRequest)} {{ MoverLeadsParaId = {MoverLeadsParaId} }}";
}

internal static class RegrasEtapas
{
    public const string MensagemNomeRepetido = "Já existe uma etapa com esse nome. Se ela estiver desativada, reative-a.";

    public const string MensagemFechamentoFixo = "Ganho e Perdido não podem ser desativadas nem reordenadas.";

    public const string MensagemUltimaAberta = "O funil precisa de pelo menos uma etapa aberta ativa.";

    public const string MensagemOrdemIncompleta = "Informe todas as etapas abertas, cada uma uma vez.";

    public const string MensagemDestinoInvalido = "Escolha outra etapa aberta e ativa para receber os leads.";

    public static string MensagemTemLeads(int quantidade) =>
        quantidade == 1
            ? "A etapa tem 1 lead. Escolha para qual etapa ele vai antes de desativar."
            : $"A etapa tem {quantidade} leads. Escolha para qual etapa eles vão antes de desativar.";

    /// <summary>
    /// Só etapas abertas são desativadas: Ganho e Perdido existem sempre, uma de cada. E sempre sobra uma
    /// aberta ativa, que é a etapa inicial.
    /// </summary>
    /// <param name="abertasAtivas">Quantas etapas abertas estão ativas, contando esta.</param>
    public static string? ConferirDesativacao(Etapa etapa, int abertasAtivas) =>
        etapa.Tipo != TipoEtapa.Aberta ? MensagemFechamentoFixo
        : etapa.Ativa && abertasAtivas <= 1 ? MensagemUltimaAberta
        : null;

    /// <summary>
    /// Numera as etapas na ordem do funil: as abertas na ordem pedida, depois Ganho e depois Perdido. As outras
    /// etapas de Ganho e Perdido, se houver desativadas, vêm no fim.
    /// </summary>
    public static void Numerar(IReadOnlyList<Etapa> abertasNaOrdem, IEnumerable<Etapa> fechamento)
    {
        var ordem = 0;
        foreach (var etapa in abertasNaOrdem.Concat(fechamento.OrderByDescending(e => e.Ativa).ThenBy(e => e.Tipo).ThenBy(e => e.Id)))
        {
            etapa.Ordem = ++ordem;
        }
    }
}

internal sealed class CriarEtapaRequestValidator : Validador<CriarEtapaRequest>
{
    public CriarEtapaRequestValidator() => RegrasNome.Aplicar(this, Etapa.TamanhoMaximoNome);
}

internal sealed class RenomearEtapaRequestValidator : Validador<RenomearEtapaRequest>
{
    public RenomearEtapaRequestValidator() => RegrasNome.Aplicar(this, Etapa.TamanhoMaximoNome);
}

internal sealed class ReordenarEtapasRequestValidator : Validador<ReordenarEtapasRequest>
{
    public ReordenarEtapasRequestValidator() =>
        RuleFor(r => r.Ids)
            .Must(ids => ids is { Count: > 0 } && ids.Distinct().Count() == ids.Count)
            .WithMessage(RegrasEtapas.MensagemOrdemIncompleta);
}
