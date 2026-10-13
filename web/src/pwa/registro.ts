// O service worker do PWA (public/sw.js, Passo 11). Só existe no build de produção: em desenvolvimento, o Vite em
// localhost:5173 nunca fica com service worker, e o que está no ar é sempre o código da pasta.

/** O tipo da mensagem que o service worker manda quando a pessoa clica numa notificação (o mesmo de public/sw.js). */
export const NOTIFICACAO_CLICADA = 'crm:notificacao-clicada'

/**
 * Registra o service worker. A falha não impede nada: no Compose de produção no próprio computador, o Chrome recusa o
 * service worker por causa do certificado local do Caddy, e a notificação nativa volta para o `new Notification`.
 */
export async function registrarServiceWorker(producao: boolean = import.meta.env.PROD) {
  if (!producao || !('serviceWorker' in navigator)) {
    return
  }
  try {
    await navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' })
  } catch {
    // Sem o service worker, a instalação fica pelo menu do navegador, e a notificação, pelo new Notification.
  }
}

/** A página é controlada pelo service worker, e a notificação nativa sai por ele. */
export function comServiceWorker() {
  return Boolean(navigator.serviceWorker?.controller)
}
