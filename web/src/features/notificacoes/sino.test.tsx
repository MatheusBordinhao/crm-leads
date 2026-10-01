import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { atendente, comSessao } from '@/test/api'
import { comCadastros, comQuadro, fichaLead } from '@/test/leads'
import { comNotificacoes, notificacao } from '@/test/notificacoes'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'
import { INTERVALO_NOTIFICACOES } from './dados'

describe('sino de notificações', () => {
  it('mostra quantas não foram lidas e a lista, com o nome do lead', async () => {
    comSessao(atendente)
    comQuadro()
    comNotificacoes([
      notificacao({ id: 2, tipo: 'nova_entrada', titulo: 'Nova entrada em um lead seu' }),
      notificacao({ id: 1, lidaEm: '2026-10-05T17:03:00Z', lead: { id: 1025, nome: 'João Lima' } }),
    ])
    const { usuario } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações: 1 não lida' }))

    const menu = await screen.findByRole('menu')
    expect(
      within(menu).getByRole('menuitem', {
        name: 'Nova entrada em um lead seu (não lida), Maria Souza, há 5 min',
      }),
    ).toBeInTheDocument()
    expect(
      within(menu).getByRole('menuitem', { name: 'Novo lead para você, João Lima, há 5 min' }),
    ).toBeInTheDocument()
  })

  it('clicar numa notificação a marca como lida e abre a ficha', async () => {
    comSessao(atendente)
    comQuadro()
    comCadastros()
    servidor.use(
      http.get('*/api/leads/:id', () => HttpResponse.json(fichaLead())),
      http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
    )
    const { marcadas } = comNotificacoes([notificacao({ id: 7 })])
    const { usuario, router } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações: 1 não lida' }))
    await usuario.click(await screen.findByRole('menuitem', { name: /Novo lead para você/ }))

    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/leads/1024')
    expect(marcadas).toEqual(['7'])
    expect(await screen.findByRole('button', { name: 'Notificações' })).toBeInTheDocument()
  })

  it('marca todas como lidas', async () => {
    comSessao(atendente)
    comQuadro()
    const { marcadas } = comNotificacoes([
      notificacao({ id: 1 }),
      notificacao({ id: 2, titulo: 'Lead passado para você', tipo: 'lead_reatribuido' }),
    ])
    const { usuario } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações: 2 não lidas' }))
    const marcarTodas = await screen.findByRole('menuitem', { name: 'Marcar todas como lidas' })
    await usuario.click(marcarTodas)

    await waitFor(() => expect(marcarTodas).toHaveAttribute('aria-disabled', 'true'))
    expect(marcadas).toEqual(['todas'])
    expect(screen.getAllByRole('menuitem', { name: /há 5 min/ })).toHaveLength(2)
    expect(screen.queryByRole('menuitem', { name: /não lida/ })).not.toBeInTheDocument()
  })

  it('sem notificações, diz que não há nenhuma', async () => {
    comSessao(atendente)
    comQuadro()
    const { usuario } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações' }))

    expect(await screen.findByText('Nenhuma notificação.')).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: 'Marcar todas como lidas' })).toHaveAttribute(
      'aria-disabled',
      'true',
    )
  })

  it('consulta a lista de novo a cada 30 segundos', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    try {
      comSessao(atendente)
      comQuadro()
      const { consultas } = comNotificacoes([])
      renderizarApp('/')
      await waitFor(() => expect(consultas).toHaveLength(1))

      await vi.advanceTimersByTimeAsync(INTERVALO_NOTIFICACOES)

      await waitFor(() => expect(consultas).toHaveLength(2))
    } finally {
      vi.useRealTimers()
    }
  })
})
