using Crm.Api.Features.Leads;
using Crm.Api.Features.Leads.Normalizacao;

namespace Crm.Api.Features.Funil;

/// <param name="Campo">Campo da requisição, como no JSON; nulo quando o problema é do lead, e não do pedido.</param>
public sealed record ErroMudancaEtapa(string? Campo, string Mensagem);

/// <summary>Regras da mudança de etapa (seção 3.4), sem banco.</summary>
public static class RegrasFunil
{
    public const string MensagemEtapaInvalida = "Etapa não encontrada ou desativada.";

    public const string MensagemMotivoObrigatorio = "Escolha o motivo da perda.";

    public const string MensagemMotivoInvalido = "Motivo de perda não encontrado ou desativado.";

    public const string MensagemMotivoSoEmPerdido = "O motivo de perda só vale para a etapa Perdido.";

    public const string MensagemDocumentoParaGanho = "Para marcar como Ganho, informe um CPF ou CNPJ válido na ficha do lead.";

    /// <summary>
    /// Confere se o lead pode ir para o <paramref name="destino"/>: Perdido exige um motivo ativo, e Ganho exige
    /// CPF/CNPJ válido quando a configuração <c>exigir_documento_para_ganho</c> está ligada.
    /// </summary>
    /// <param name="motivoPerdaId">O motivo pedido.</param>
    /// <param name="motivo">O motivo encontrado com esse id, ou nulo se não existe.</param>
    public static ErroMudancaEtapa? Conferir(
        Lead lead,
        Etapa destino,
        int? motivoPerdaId,
        MotivoPerda? motivo,
        string? detalhe,
        bool exigirDocumentoParaGanho)
    {
        if (destino.Tipo == TipoEtapa.Perdido)
        {
            if (motivoPerdaId is null)
            {
                return new ErroMudancaEtapa("motivoPerdaId", MensagemMotivoObrigatorio);
            }

            if (motivo is not { Ativo: true })
            {
                return new ErroMudancaEtapa("motivoPerdaId", MensagemMotivoInvalido);
            }
        }
        else if (motivoPerdaId is not null || detalhe is not null)
        {
            return new ErroMudancaEtapa(motivoPerdaId is not null ? "motivoPerdaId" : "motivoPerdaDetalhe", MensagemMotivoSoEmPerdido);
        }

        if (destino.Tipo == TipoEtapa.Ganho && exigirDocumentoParaGanho && !Documentos.Valido(lead.CpfCnpj))
        {
            return new ErroMudancaEtapa(null, MensagemDocumentoParaGanho);
        }

        return null;
    }

    /// <summary>
    /// Leva o lead para o <paramref name="destino"/> e devolve a linha do histórico. Ganho grava
    /// <c>convertido_em</c>; Perdido grava <c>perdido_em</c> e o motivo; sair deles limpa esses campos, e o
    /// histórico guarda tudo. A primeira saída da etapa inicial conta como primeiro contato, a não ser quando
    /// a mudança não é um contato (<paramref name="contaComoContato"/>), como a reabertura por uma nova entrada
    /// ou a mudança em lote ao desativar uma etapa.
    /// </summary>
    public static HistoricoEtapa Aplicar(
        Lead lead,
        Etapa destino,
        int etapaInicialId,
        MotivoPerda? motivo,
        string? detalhe,
        Guid? usuarioId,
        DateTimeOffset agora,
        bool contaComoContato)
    {
        var perdido = destino.Tipo == TipoEtapa.Perdido;
        if (contaComoContato && lead.EtapaId == etapaInicialId && destino.Id != etapaInicialId)
        {
            lead.RegistrarPrimeiroContato(agora);
        }

        var historico = new HistoricoEtapa
        {
            LeadId = lead.Id,
            EtapaAnteriorId = lead.EtapaId,
            EtapaNovaId = destino.Id,
            MotivoPerdaId = perdido ? motivo?.Id : null,
            MotivoPerdaDetalhe = perdido ? detalhe : null,
            UsuarioId = usuarioId,
            OcorridoEm = agora,
        };

        lead.EtapaId = destino.Id;
        lead.ConvertidoEm = destino.Tipo == TipoEtapa.Ganho ? agora : null;
        lead.PerdidoEm = perdido ? agora : null;
        lead.MotivoPerdaId = perdido ? motivo?.Id : null;
        lead.MotivoPerdaDetalhe = perdido ? detalhe : null;
        lead.AtualizadoEm = agora;
        return historico;
    }
}
