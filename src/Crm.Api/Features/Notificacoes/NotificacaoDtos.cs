namespace Crm.Api.Features.Notificacoes;

public sealed record LeadDaNotificacao(long Id, string Nome);

/// <param name="Titulo">O que aconteceu, sem dados do lead.</param>
/// <param name="Lead">O lead, com o nome atual. Nulo quando a notificação não é de um lead.</param>
/// <param name="ImportacaoId">A importação que a notificação abre: só no resumo para o admin que importou.</param>
/// <param name="LidaEm">Nula enquanto não foi lida.</param>
public sealed record NotificacaoResponse(
    long Id,
    TipoNotificacao Tipo,
    string Titulo,
    LeadDaNotificacao? Lead,
    long? ImportacaoId,
    DateTimeOffset CriadoEm,
    DateTimeOffset? LidaEm);

/// <param name="NaoLidas">Quantas ainda não foram lidas, contando as que ficaram fora da lista.</param>
/// <param name="Itens">As mais recentes, da mais nova para a mais antiga.</param>
public sealed record NotificacoesResponse(int NaoLidas, IReadOnlyList<NotificacaoResponse> Itens);
