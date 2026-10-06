/** Onde o navegador guarda que a pessoa já respondeu ao pedido de permissão. */
const CHAVE_RESPOSTA = 'crm:avisos-do-navegador'

/**
 * A notificação nativa (seção 3.6) só é oferecida fora do celular: o Chrome do Android só a mostra com um service
 * worker, e o Safari do iPhone, só no app instalado. No celular, ela chega com o PWA do Passo 11.
 */
export function avisosSuportados() {
  return 'Notification' in window && !/Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent)
}

/** A permissão ainda não foi dada nem negada: o sino oferece ativar. */
export function permissaoPendente() {
  return avisosSuportados() && Notification.permission === 'default'
}

/** O pedido de permissão aparece uma vez: até a pessoa ativar ou responder "Agora não". */
export function devePerguntar() {
  if (!permissaoPendente()) {
    return false
  }
  try {
    return localStorage.getItem(CHAVE_RESPOSTA) === null
  } catch {
    // Sem armazenamento (navegação privada, por exemplo), o pedido não aparece; o sino continua oferecendo.
    return false
  }
}

export function lembrarResposta() {
  try {
    localStorage.setItem(CHAVE_RESPOSTA, 'respondido')
  } catch {
    // Sem armazenamento, não há como lembrar; o pedido não aparece de novo nesta aba.
  }
}

/** Pede a permissão ao navegador. O Firefox e o Safari só mostram o pedido depois de um clique da pessoa. */
export function pedirPermissao() {
  lembrarResposta()
  return Notification.requestPermission()
}

/** A notificação nativa vale agora: a aba está escondida, e a permissão foi dada. */
export function podeMostrar() {
  return (
    avisosSuportados() &&
    Notification.permission === 'granted' &&
    document.visibilityState === 'hidden'
  )
}

/**
 * Mostra a notificação nativa só com o título, que nunca leva dados do lead: o nome dele não vai para a central de
 * notificações do sistema. Com a aba na frente, não mostra nada; o sino já mostra.
 */
export function mostrarNotificacao(titulo: string, id: number, aoClicar: () => void) {
  if (!podeMostrar()) {
    return
  }
  try {
    const notificacao = new Notification(titulo, {
      tag: `crm-notificacao-${id}`,
      icon: '/favicon.svg',
      lang: 'pt-BR',
    })
    notificacao.onclick = () => {
      window.focus()
      notificacao.close()
      aoClicar()
    }
  } catch {
    // O navegador recusou a notificação; o sino continua mostrando.
  }
}
