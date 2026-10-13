import { comServiceWorker } from '@/pwa/registro'

/** Onde o navegador guarda que a pessoa já respondeu ao pedido de permissão. */
const CHAVE_RESPOSTA = 'crm:avisos-do-navegador'

const CELULAR = /Android|iPhone|iPad|iPod|Mobile/i

/**
 * A notificação nativa (seção 3.6). Com o service worker do PWA, que só existe no build de produção, ela sai por ele,
 * no computador e no celular. Sem ele, só no computador, pelo `new Notification`: o Chrome do Android só a mostra pelo
 * service worker, e o Safari do iPhone, só no app instalado.
 */
export function avisosSuportados() {
  return 'Notification' in window && (comServiceWorker() || !CELULAR.test(navigator.userAgent))
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
 *
 * Pelo service worker, o clique chega a ele (public/sw.js), que avisa a tela ou, com o CRM fechado, abre o
 * `endereco`. Sem ele, o clique chama `aoClicar`.
 */
export function mostrarNotificacao(
  titulo: string,
  id: number,
  endereco: string,
  aoClicar: () => void,
) {
  if (!podeMostrar()) {
    return
  }
  const opcoes = { tag: `crm-notificacao-${id}`, icon: '/icone-192.png', lang: 'pt-BR' }
  if (comServiceWorker()) {
    void navigator.serviceWorker.ready
      .then((registro) =>
        registro.showNotification(titulo, {
          ...opcoes,
          badge: '/icone-notificacao.png',
          data: { id, url: endereco },
        }),
      )
      .catch(() => {
        // O navegador recusou a notificação; o sino continua mostrando.
      })
    return
  }
  try {
    const notificacao = new Notification(titulo, opcoes)
    notificacao.onclick = () => {
      window.focus()
      notificacao.close()
      aoClicar()
    }
  } catch {
    // O navegador recusou a notificação; o sino continua mostrando.
  }
}
