import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

/** Endereço do hub na API. Em desenvolvimento, o Vite o repassa para a API, com o WebSocket. */
export const ROTA_HUB = '/hubs/notificacoes'

/**
 * Os avisos que o hub manda (seção 3.6), com os nomes do IClienteTempoReal da API. Só levam ids: a tela busca os
 * dados na API, que aplica a visibilidade de sempre.
 */
export type AvisosTempoReal = {
  /** Esses leads mudaram. Sem ids: um lead da pessoa passou para outra; recarregue o que estiver aberto. */
  LeadsAlterados: (ids: number[]) => void
  /** Uma notificação nova para a pessoa. */
  NotificacaoNova: (id: number) => void
  /** As etapas ou os motivos de perda mudaram. */
  FunilAlterado: () => void
}

/** O que a tela usa da conexão. O hub falso dos testes (test/hubFalso.ts) faz o mesmo. */
export type ConexaoTempoReal = {
  ao<K extends keyof AvisosTempoReal>(aviso: K, tratar: AvisosTempoReal[K]): void
  /** Chamado quando a conexão cai ou o servidor a encerra, mas não quando um início falha. */
  aoFechar(tratar: () => void): void
  iniciar(): Promise<void>
  parar(): Promise<void>
}

export type CriarConexao = () => ConexaoTempoReal

/**
 * A conexão de verdade, com o @microsoft/signalr. A reconexão fica com o TempoReal: a automática do SignalR não tenta de
 * novo quando o primeiro início falha nem quando o servidor encerra a conexão (sessão encerrada, perfil trocado).
 */
export const criarConexao: CriarConexao = () => {
  const conexao = new HubConnectionBuilder()
    .withUrl(ROTA_HUB)
    .configureLogging(LogLevel.Warning)
    .build()
  return {
    ao: (aviso, tratar) => conexao.on(aviso, tratar),
    aoFechar: (tratar) => conexao.onclose(() => tratar()),
    iniciar: () => conexao.start(),
    parar: () => conexao.stop(),
  }
}
