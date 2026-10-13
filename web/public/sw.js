// Service worker do CRM (Passo 11), registrado só no build de produção (src/pwa/registro.ts).
//
// Não tem evento fetch nem cache: não intercepta nenhuma requisição, então nunca serve uma versão velha da tela nem
// mexe na API e no tempo real. Serve para a instalação como app e para a notificação nativa, que no Chrome do Android
// só aparece por aqui. A notificação leva só o título, sem dado do lead (seção 3.6); nos dados dela vão o id da
// notificação e o endereço que o clique abre.

/** O tipo da mensagem que avisa a janela do CRM do clique (o mesmo de src/pwa/registro.ts). */
const NOTIFICACAO_CLICADA = 'crm:notificacao-clicada'

self.addEventListener('install', () => self.skipWaiting())

self.addEventListener('activate', (evento) => evento.waitUntil(self.clients.claim()))

self.addEventListener('notificationclick', (evento) => {
  evento.notification.close()
  const { id, url } = evento.notification.data ?? {}
  evento.waitUntil(abrir(id, url))
})

/**
 * Com o CRM aberto, traz a janela para a frente e avisa a tela, que marca a notificação como lida e abre a ficha, como
 * pelo sino. Com ele fechado, abre o endereço da notificação e a marca como lida daqui, com o cookie da sessão.
 */
async function abrir(id, url) {
  const [janela] = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
  if (janela) {
    await janela.focus().catch(() => {})
    janela.postMessage({ tipo: NOTIFICACAO_CLICADA, id })
    return
  }

  await self.clients.openWindow(url ?? '/')
  if (typeof id === 'number') {
    await fetch(`/api/notificacoes/${id}/lida`, {
      method: 'POST',
      credentials: 'same-origin',
    }).catch(() => {})
  }
}
