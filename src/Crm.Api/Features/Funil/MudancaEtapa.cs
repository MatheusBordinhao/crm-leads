using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;
using Crm.Api.Infra.Configuracao;
using Crm.Api.Infra.Dados;
using Crm.Api.Infra.Web;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Features.Funil;

/// <param name="MotivoPerdaId">Obrigatório quando a etapa é Perdido; nulo nas outras.</param>
/// <param name="MotivoPerdaDetalhe">Opcional, só em Perdido.</param>
public sealed record MudarEtapaRequest(int EtapaId, int? MotivoPerdaId, string? MotivoPerdaDetalhe)
{
    /// <summary>Sem o detalhe, que é texto livre: o MVC imprime os argumentos da action no log de nível Trace.</summary>
    public override string ToString() =>
        $"{nameof(MudarEtapaRequest)} {{ EtapaId = {EtapaId}, MotivoPerdaId = {MotivoPerdaId} }}";
}

internal sealed class MudarEtapaRequestValidator : Validador<MudarEtapaRequest>
{
    public MudarEtapaRequestValidator()
    {
        RuleFor(r => r.EtapaId).GreaterThan(0).WithMessage("Escolha a etapa.");
        RuleFor(r => r.MotivoPerdaDetalhe)
            .Must(d => d is null || d.Trim().Length <= Lead.TamanhoMaximoDetalhePerda)
            .WithMessage($"O detalhe pode ter até {Lead.TamanhoMaximoDetalhePerda} caracteres.");
    }
}

/// <summary>
/// Toda mudança de etapa passa por aqui (seção 3.4): a da tela, a reabertura de um lead perdido por uma nova
/// entrada e a mudança em lote ao desativar uma etapa. Cada mudança grava <c>historico_etapas</c>.
/// </summary>
public sealed class MudancaEtapa(CrmDbContext db, Configuracoes configuracoes, TimeProvider relogio)
{
    /// <summary>
    /// Mudança pedida na tela. O lead já precisa estar travado (<see cref="TravasLeads.TravarLeadAsync"/>) e
    /// carregado pelo contexto; as alterações são gravadas no próximo <c>SaveChanges</c>. Ir para a etapa em que
    /// o lead já está não muda nada.
    /// </summary>
    public async Task<ErroMudancaEtapa?> MudarAsync(Lead lead, MudarEtapaRequest pedido, Guid? usuarioId, CancellationToken ct)
    {
        var destino = await db.Etapas.AtivaParaMoverAsync(pedido.EtapaId, ct);
        if (destino is null)
        {
            return new ErroMudancaEtapa("etapaId", RegrasFunil.MensagemEtapaInvalida);
        }

        if (destino.Id == lead.EtapaId)
        {
            return null;
        }

        var motivo = pedido.MotivoPerdaId is { } motivoId
            ? await db.MotivosPerda.SingleOrDefaultAsync(m => m.Id == motivoId, ct)
            : null;
        var detalhe = Textos.Limpar(pedido.MotivoPerdaDetalhe);
        var exigirDocumento = destino.Tipo == TipoEtapa.Ganho && await configuracoes.ExigirDocumentoParaGanhoAsync(ct);
        if (RegrasFunil.Conferir(lead, destino, pedido.MotivoPerdaId, motivo, detalhe, exigirDocumento) is { } erro)
        {
            return erro;
        }

        var inicial = await db.Etapas.InicialAsync(ct);
        Registrar(lead, destino, inicial.Id, motivo, detalhe, usuarioId, relogio.GetUtcNow(), contaComoContato: true);
        return null;
    }

    /// <summary>
    /// Aplica a mudança no lead e grava o histórico, sem conferir as regras de Perdido e Ganho. Serve para a
    /// reabertura de um lead perdido e para a mudança em lote, que não contam como contato.
    /// </summary>
    internal void Registrar(
        Lead lead,
        Etapa destino,
        int etapaInicialId,
        MotivoPerda? motivo,
        string? detalhe,
        Guid? usuarioId,
        DateTimeOffset agora,
        bool contaComoContato) =>
        db.HistoricoEtapas.Add(RegrasFunil.Aplicar(lead, destino, etapaInicialId, motivo, detalhe, usuarioId, agora, contaComoContato));
}

internal static class FunilExtensions
{
    public static IServiceCollection AddFunil(this IServiceCollection services) => services.AddScoped<MudancaEtapa>();
}
