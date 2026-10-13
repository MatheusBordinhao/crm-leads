import { describe, expect, it, vi } from 'vitest'
import codigo from '../../public/sw.js?raw'
import { NOTIFICACAO_CLICADA } from './registro'

type Ouvinte = (evento: unknown) => void
type Janela = { focus: () => Promise<unknown>; postMessage: (mensagem: unknown) => void }

/** Roda o public/sw.js num `self` falso, com as janelas do CRM abertas, e devolve o que ele ouve e chama. */
function carregarServiceWorker(janelas: Janela[] = []) {
  const ouvintes = new Map<string, Ouvinte>()
  const clients = {
    claim: vi.fn(async () => {}),
    matchAll: vi.fn(async () => janelas),
    openWindow: vi.fn(async () => null),
  }
  const self = {
    addEventListener: (tipo: string, ouvinte: Ouvinte) => ouvintes.set(tipo, ouvinte),
    skipWaiting: vi.fn(async () => {}),
    clients,
  }
  const fetch = vi.fn(async () => new Response(null, { status: 204 }))
  new Function('self', 'fetch', codigo)(self, fetch)
  return { ouvintes, clients, self, fetch }
}

/** Dispara um evento no service worker e espera o que ele pediu para esperar (`waitUntil`). */
async function disparar(ouvintes: Map<string, Ouvinte>, tipo: string, evento: object = {}) {
  const esperas: Promise<unknown>[] = []
  ouvintes.get(tipo)!({ ...evento, waitUntil: (espera: Promise<unknown>) => esperas.push(espera) })
  await Promise.all(esperas)
}

function notificacao(data: unknown) {
  return { data, close: vi.fn() }
}

describe('service worker', () => {
  it('não intercepta requisições nem guarda cache', () => {
    const { ouvintes } = carregarServiceWorker()

    expect(ouvintes.has('fetch')).toBe(false)
    expect(codigo).not.toMatch(/caches\./)
  })

  it('assume na hora as abas abertas, sem esperar elas fecharem', async () => {
    const { ouvintes, self, clients } = carregarServiceWorker()

    ouvintes.get('install')!({})
    await disparar(ouvintes, 'activate')

    expect(self.skipWaiting).toHaveBeenCalled()
    expect(clients.claim).toHaveBeenCalled()
  })

  it('com o CRM aberto, o clique traz a janela para a frente e avisa a tela', async () => {
    const janela = { focus: vi.fn(async () => undefined), postMessage: vi.fn() }
    const { ouvintes, clients, fetch } = carregarServiceWorker([janela])
    const clicada = notificacao({ id: 9, url: '/leads/1024' })

    await disparar(ouvintes, 'notificationclick', { notification: clicada })

    expect(clicada.close).toHaveBeenCalled()
    expect(janela.focus).toHaveBeenCalled()
    expect(janela.postMessage).toHaveBeenCalledWith({ tipo: NOTIFICACAO_CLICADA, id: 9 })
    // Quem marca como lida e abre a ficha é a tela, como pelo sino.
    expect(clients.openWindow).not.toHaveBeenCalled()
    expect(fetch).not.toHaveBeenCalled()
  })

  it('com o CRM fechado, o clique abre a ficha e marca a notificação como lida', async () => {
    const { ouvintes, clients, fetch } = carregarServiceWorker([])

    await disparar(ouvintes, 'notificationclick', {
      notification: notificacao({ id: 9, url: '/leads/1024' }),
    })

    expect(clients.openWindow).toHaveBeenCalledWith('/leads/1024')
    expect(fetch).toHaveBeenCalledWith('/api/notificacoes/9/lida', {
      method: 'POST',
      credentials: 'same-origin',
    })
  })
})
