import { act, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { NOTIFICACAO_CLICADA } from '@/pwa/registro'
import { atendente, comSessao } from '@/test/api'
import { comCadastros, comQuadro, fichaLead } from '@/test/leads'
import { comNotificacoes, notificacao } from '@/test/notificacoes'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

/** A Notification do navegador, que o jsdom não tem. Guarda as notificações mostradas. */
class NotificacaoFalsa {
  static permission: NotificationPermission = 'default'
  static readonly criadas: NotificacaoFalsa[] = []
  static readonly requestPermission = vi.fn(async () => {
    NotificacaoFalsa.permission = 'granted'
    return NotificacaoFalsa.permission
  })

  onclick: (() => void) | null = null
  readonly close = vi.fn()
  readonly titulo: string
  readonly opcoes?: NotificationOptions

  constructor(titulo: string, opcoes?: NotificationOptions) {
    this.titulo = titulo
    this.opcoes = opcoes
    NotificacaoFalsa.criadas.push(this)
  }
}

let visibilidade: DocumentVisibilityState = 'visible'

beforeEach(() => {
  NotificacaoFalsa.permission = 'default'
  NotificacaoFalsa.criadas.length = 0
  NotificacaoFalsa.requestPermission.mockClear()
  vi.stubGlobal('Notification', NotificacaoFalsa)
  visibilidade = 'visible'
  vi.spyOn(document, 'visibilityState', 'get').mockImplementation(() => visibilidade)
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  localStorage.clear()
  Reflect.deleteProperty(navigator, 'serviceWorker')
})

/**
 * O service worker do PWA, que o jsdom não tem: a página fica controlada por ele, que guarda as notificações mostradas.
 * `clicar` simula a mensagem que ele manda quando a pessoa clica numa delas (public/sw.js).
 */
function comServiceWorker() {
  const mostradas: { titulo: string; opcoes?: NotificationOptions }[] = []
  const registro = {
    showNotification: vi.fn(async (titulo: string, opcoes?: NotificationOptions) => {
      mostradas.push({ titulo, opcoes })
    }),
  }
  const servico = Object.assign(new EventTarget(), {
    controller: {},
    ready: Promise.resolve(registro),
  })
  Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: servico })
  return {
    mostradas,
    clicar: (id: number) =>
      servico.dispatchEvent(
        new MessageEvent('message', { data: { tipo: NOTIFICACAO_CLICADA, id } }),
      ),
  }
}

/** A pessoa já respondeu ao pedido de permissão: o aviso com "Ativar" não aparece. */
function jaRespondeu() {
  localStorage.setItem('crm:avisos-do-navegador', 'respondido')
}

/** A ficha do lead 1024, que a notificação abre. */
function comFicha() {
  comCadastros()
  servidor.use(
    http.get('*/api/leads/:id', () => HttpResponse.json(fichaLead())),
    http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
  )
}

describe('notificação nativa do navegador', () => {
  it('com a aba escondida e a permissão dada, mostra só o título, e o clique abre a ficha', async () => {
    NotificacaoFalsa.permission = 'granted'
    comSessao(atendente)
    comQuadro()
    comFicha()
    const { adicionar, marcadas } = comNotificacoes([])
    const { hub, router } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    visibilidade = 'hidden'
    adicionar(notificacao({ id: 9, titulo: 'Novo lead para você' }))
    hub.emitir('NotificacaoNova', 9)

    await waitFor(() => expect(NotificacaoFalsa.criadas).toHaveLength(1))
    const [nativa] = NotificacaoFalsa.criadas
    expect(nativa.titulo).toBe('Novo lead para você')
    expect(nativa.opcoes?.body).toBeUndefined()
    expect(JSON.stringify(nativa.opcoes)).not.toContain('Maria')
    expect(
      await screen.findByRole('button', { name: 'Notificações: 1 não lida' }),
    ).toBeInTheDocument()

    visibilidade = 'visible'
    act(() => nativa.onclick?.())

    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/leads/1024')
    expect(nativa.close).toHaveBeenCalled()
    await waitFor(() => expect(marcadas).toEqual(['9']))
  })

  it('com o service worker, a notificação sai por ele, só com o título, e o clique abre a ficha', async () => {
    NotificacaoFalsa.permission = 'granted'
    const sw = comServiceWorker()
    comSessao(atendente)
    comQuadro()
    comFicha()
    const { adicionar, marcadas } = comNotificacoes([])
    const { hub, router } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    visibilidade = 'hidden'
    adicionar(notificacao({ id: 9, titulo: 'Novo lead para você' }))
    hub.emitir('NotificacaoNova', 9)

    await waitFor(() => expect(sw.mostradas).toHaveLength(1))
    const [mostrada] = sw.mostradas
    expect(mostrada.titulo).toBe('Novo lead para você')
    expect(mostrada.opcoes).toMatchObject({
      tag: 'crm-notificacao-9',
      data: { id: 9, url: '/leads/1024' },
    })
    expect(mostrada.opcoes?.body).toBeUndefined()
    expect(JSON.stringify(mostrada.opcoes)).not.toContain('Maria')
    expect(NotificacaoFalsa.criadas).toHaveLength(0)

    visibilidade = 'visible'
    act(() => {
      sw.clicar(9)
    })

    // Até a lista de notificações voltar, o funil continua na tela, e o teste confere o título a cada mudança no DOM.
    // Pelo queryBy, a tentativa que falha não monta erro: com o getBy ou o findBy, a biblioteca formataria a página
    // inteira a cada uma, e, com a máquina lenta, isso atrasava a própria ficha.
    await waitFor(() =>
      expect(screen.queryByText('Maria Souza', { selector: 'h1' })).toBeInTheDocument(),
    )
    expect(router.state.location.pathname).toBe('/leads/1024')
    await waitFor(() => expect(marcadas).toEqual(['9']))
  })

  it('com a aba na frente, o sino mostra, e a notificação nativa não aparece', async () => {
    NotificacaoFalsa.permission = 'granted'
    comSessao(atendente)
    comQuadro()
    const { adicionar } = comNotificacoes([])
    const { hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    adicionar(notificacao({ id: 9 }))
    hub.emitir('NotificacaoNova', 9)

    expect(
      await screen.findByRole('button', { name: 'Notificações: 1 não lida' }),
    ).toBeInTheDocument()
    expect(NotificacaoFalsa.criadas).toHaveLength(0)
  })

  it('sem a permissão, a notificação nativa não aparece', async () => {
    NotificacaoFalsa.permission = 'denied'
    comSessao(atendente)
    comQuadro()
    const { adicionar } = comNotificacoes([])
    const { hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    visibilidade = 'hidden'
    adicionar(notificacao({ id: 9 }))
    hub.emitir('NotificacaoNova', 9)

    expect(
      await screen.findByRole('button', { name: 'Notificações: 1 não lida' }),
    ).toBeInTheDocument()
    expect(NotificacaoFalsa.criadas).toHaveLength(0)
  })
})

describe('pedido de permissão', () => {
  it('aparece depois do login, e "Ativar" pede a permissão ao navegador', async () => {
    comSessao(atendente)
    comQuadro()
    const { usuario } = renderizarApp('/funil')

    expect(await screen.findByText('Avisos do navegador')).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Ativar' }))

    expect(NotificacaoFalsa.requestPermission).toHaveBeenCalledOnce()
    expect(localStorage.getItem('crm:avisos-do-navegador')).not.toBeNull()
  })

  it('"Agora não" guarda a resposta, e o pedido não aparece de novo', async () => {
    comSessao(atendente)
    comQuadro()
    const { usuario } = renderizarApp('/funil')
    expect(await screen.findByText('Avisos do navegador')).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Agora não' }))

    expect(localStorage.getItem('crm:avisos-do-navegador')).not.toBeNull()
    expect(NotificacaoFalsa.requestPermission).not.toHaveBeenCalled()
  })

  it('com a resposta guardada, o pedido não aparece, e o sino oferece ativar', async () => {
    jaRespondeu()
    comSessao(atendente)
    comQuadro()
    comNotificacoes([])
    const { usuario, hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))
    expect(screen.queryByText('Avisos do navegador')).not.toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Notificações' }))
    const menu = within(await screen.findByRole('menu'))
    await usuario.click(menu.getByRole('menuitem', { name: 'Ativar avisos do navegador' }))

    expect(NotificacaoFalsa.requestPermission).toHaveBeenCalledOnce()
    await waitFor(() =>
      expect(
        menu.queryByRole('menuitem', { name: 'Ativar avisos do navegador' }),
      ).not.toBeInTheDocument(),
    )
  })

  it('no celular, com o service worker do PWA, o pedido aparece', async () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(
      'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/141.0 Mobile Safari/537.36',
    )
    comServiceWorker()
    comSessao(atendente)
    comQuadro()

    renderizarApp('/funil')

    expect(await screen.findByText('Avisos do navegador')).toBeInTheDocument()
  })

  it('no celular sem o service worker, nem o pedido nem o sino oferecem os avisos do navegador', async () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue(
      'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/141.0 Mobile Safari/537.36',
    )
    comSessao(atendente)
    comQuadro()
    comNotificacoes([])
    const { usuario, hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    await usuario.click(screen.getByRole('button', { name: 'Notificações' }))

    expect(await screen.findByRole('menu')).toBeInTheDocument()
    expect(screen.queryByText('Avisos do navegador')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('menuitem', { name: 'Ativar avisos do navegador' }),
    ).not.toBeInTheDocument()
  })
})
