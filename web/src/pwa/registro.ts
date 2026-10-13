// O service worker do PWA (public/sw.js, Passo 11). Só existe no build de produção: em desenvolvimento, o Vite em
// localhost:5173 nunca fica com service worker, e o que está no ar é sempre o código da pasta.

/** O tipo da mensagem que o service worker manda quando a pessoa clica numa notificação (o mesmo de public/sw.js). */
export const NOTIFICACAO_CLICADA = 'crm:notificacao-clicada'

type Endereco = Pick<Location, 'protocol' | 'hostname'>

/**
 * O Compose de produção no próprio computador (`https://localhost`, README › Publicação): o certificado vem da autoridade
 * local do Caddy, em que o navegador não confia, e o Chrome recusa o service worker, com um erro no console a cada
 * página. Ali, o registro nem é tentado.
 */
function certificadoLocalDoCaddy({ protocol, hostname }: Endereco) {
  return protocol === 'https:' && hostname === 'localhost'
}

/**
 * Registra o service worker. Sem ele, nada para: a instalação fica pelo menu do navegador, e a notificação nativa, pelo
 * `new Notification`.
 */
export async function registrarServiceWorker(
  producao: boolean = import.meta.env.PROD,
  endereco: Endereco = location,
) {
  if (!producao || !('serviceWorker' in navigator) || certificadoLocalDoCaddy(endereco)) {
    return
  }
  try {
    await navigator.serviceWorker.register('/sw.js', { scope: '/', updateViaCache: 'none' })
  } catch {
    // Um navegador que recusa o service worker continua com o new Notification.
  }
}

/** A página é controlada pelo service worker, e a notificação nativa sai por ele. */
export function comServiceWorker() {
  return Boolean(navigator.serviceWorker?.controller)
}
