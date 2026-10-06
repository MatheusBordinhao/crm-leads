import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { atendente, comSessao, problema } from '@/test/api'
import { comCadastros, comQuadro, fichaLead } from '@/test/leads'
import { comNotificacoes, notificacao } from '@/test/notificacoes'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

describe('conexão do tempo real', () => {
  it('com sessão e senha definitiva, conecta ao hub', async () => {
    comSessao(atendente)
    comQuadro()

    const { hub } = renderizarApp('/funil')

    await waitFor(() => expect(hub.estado.conectada).toBe(true))
    expect(hub.estado.inicios).toBe(1)
  })

  it('com a senha provisória, não conecta', async () => {
    comSessao({ ...atendente, trocarSenha: true })

    const { hub } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Trocar senha' })).toBeInTheDocument()
    expect(hub.estado.inicios).toBe(0)
  })

  it('sem sessão, não conecta', async () => {
    comSessao(null)

    const { hub } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(hub.estado.inicios).toBe(0)
  })

  it('ao sair, desconecta', async () => {
    comSessao(atendente)
    comQuadro()
    servidor.use(http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })))
    const { hub, usuario } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    await usuario.click(screen.getByRole('button', { name: 'Adriana' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Sair' }))

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(hub.estado.conectada).toBe(false)
    expect(hub.estado.paradas).toBeGreaterThan(0)
  })

  it('ao reconectar depois de uma queda, recarrega o que está na tela', async () => {
    comSessao(atendente)
    const pedidos = comQuadro()
    const { hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))
    await waitFor(() => expect(pedidos).toHaveLength(1))

    hub.fechar()

    await waitFor(() => expect(hub.estado.inicios).toBe(2))
    await waitFor(() => expect(pedidos).toHaveLength(2))
  })

  it('se a sessão acabou, a reconexão leva ao login', async () => {
    comSessao(atendente)
    comQuadro()
    const { hub, router } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    comSessao(null)
    hub.fechar()

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(hub.estado.inicios).toBe(1)
  })

  it('com a API fora do ar, a tela continua como está e a conexão tenta de novo', async () => {
    comSessao(atendente)
    comQuadro()
    const { hub } = renderizarApp('/funil')
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    servidor.use(http.get('*/api/auth/eu', () => HttpResponse.error()))
    hub.estado.falharInicio = true
    hub.fechar()

    await waitFor(() => expect(hub.estado.inicios).toBe(2))
    expect(screen.getByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    expect(screen.queryByText('Não foi possível falar com o servidor.')).not.toBeInTheDocument()
  })

  it('a notificação de um lead que passou para outra pessoa some do sino dela', async () => {
    comSessao(atendente)
    comQuadro()
    comNotificacoes([notificacao({ id: 7 })])
    const { hub } = renderizarApp('/funil')
    expect(
      await screen.findByRole('button', { name: 'Notificações: 1 não lida' }),
    ).toBeInTheDocument()
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    // A API já não lista a notificação de um lead que a pessoa não vê.
    servidor.use(
      http.get('*/api/notificacoes', () => HttpResponse.json({ naoLidas: 0, itens: [] })),
    )
    hub.emitir('LeadsAlterados', [])

    expect(await screen.findByRole('button', { name: 'Notificações' })).toBeInTheDocument()
  })

  it('o sino mostra o nome atual do lead de uma notificação', async () => {
    comSessao(atendente)
    comQuadro()
    comNotificacoes([notificacao({ id: 7 })])
    const { hub, usuario } = renderizarApp('/funil')
    const sino = await screen.findByRole('button', { name: 'Notificações: 1 não lida' })
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    comNotificacoes([notificacao({ id: 7, lead: { id: 1024, nome: 'Maria Souza Lima' } })])
    hub.emitir('LeadsAlterados', [1024])
    await usuario.click(sino)

    expect(await screen.findByRole('menuitem', { name: /Maria Souza Lima/ })).toBeInTheDocument()
  })

  it('um lead que passou para outra pessoa some da ficha aberta, pelo aviso sem ids', async () => {
    comSessao(atendente)
    comCadastros()
    let ficha = HttpResponse.json(fichaLead())
    servidor.use(
      http.get('*/api/leads/:id', () => ficha.clone()),
      http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
    )
    const { hub } = renderizarApp('/leads/1024')
    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    ficha = problema(404)
    hub.emitir('LeadsAlterados', [])

    expect(await screen.findByRole('heading', { name: 'Lead não encontrado' })).toBeInTheDocument()
  })
})
