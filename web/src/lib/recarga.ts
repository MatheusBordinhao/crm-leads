// A tela que não carrega (Lote 4B). As telas são carregadas sob demanda (`tela()`, em rotas.tsx): depois de uma
// publicação nova, quem está com o CRM aberto pode pedir o arquivo de uma tela que não existe mais no servidor (404),
// ou a conexão pode cair no meio. A página recarrega sozinha uma vez, já na versão nova; se falhar de novo em menos de
// um minuto, o ErroDaTela mostra o erro, com o botão de recarregar, sem recarga em laço.

/** A chave, no sessionStorage, da hora da última recarga automática desta aba. */
export const CHAVE_RECARGA = 'crm:recarga-da-tela'

/** O intervalo mínimo entre duas recargas automáticas na mesma aba: menos que isso já não é versão nova, é laço. */
export const INTERVALO_ENTRE_RECARGAS = 60_000

/** A falha ao carregar o arquivo de uma tela (o import do `lazy` das rotas). */
export class ErroAoCarregarTela extends Error {
  /** Se a página recarrega sozinha. Decidido uma vez, quando a tela falhou (`tela()`, em rotas.tsx). */
  readonly recarregarSozinha: boolean

  constructor(causa: unknown, recarregarSozinha = false) {
    super('Não foi possível carregar a tela.', { cause: causa })
    this.name = 'ErroAoCarregarTela'
    this.recarregarSozinha = recarregarSozinha
  }
}

/**
 * Se a página pode recarregar sozinha: nenhuma recarga automática nesta aba no último minuto. Sem o sessionStorage
 * (bloqueado ou com erro), não pode: sem a marca, nada pararia um laço.
 */
export function podeRecarregarSozinha(agora = Date.now()) {
  try {
    const ultima = Number(sessionStorage.getItem(CHAVE_RECARGA) ?? 0)
    if (!Number.isFinite(ultima)) {
      return true
    }
    return agora - ultima >= INTERVALO_ENTRE_RECARGAS
  } catch {
    return false
  }
}

/** Marca a hora da recarga automática. Devolve se conseguiu: sem a marca, a página não recarrega sozinha. */
export function marcarRecarga(agora = Date.now()) {
  try {
    sessionStorage.setItem(CHAVE_RECARGA, String(agora))
    return true
  } catch {
    return false
  }
}

/** Recarrega a página. Fica num módulo próprio para os testes a trocarem por `vi.mock`: o jsdom não recarrega. */
export function recarregarPagina() {
  window.location.reload()
}
