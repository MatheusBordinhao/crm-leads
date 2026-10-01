import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { admin, atendente, comSessao } from '@/test/api'
import { comImportacao } from '@/test/importacao'
import { comCadastros, comLista, comQuadro, fichaLead, leadResumo } from '@/test/leads'
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

  it('o resumo da importação abre o resultado dela', async () => {
    comSessao(admin)
    comQuadro()
    comImportacao()
    const { marcadas } = comNotificacoes([
      notificacao({
        id: 9,
        tipo: 'importacao_concluida',
        titulo: 'Importação concluída: 3 leads novos, 1 vinculado, 0 já importados e 1 com erro',
        lead: null,
        importacaoId: 12,
      }),
    ])
    const { usuario, router } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações: 1 não lida' }))
    await usuario.click(await screen.findByRole('menuitem', { name: /Importação concluída/ }))

    expect(await screen.findByRole('heading', { name: 'leads-meta.csv' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/importacao/12')
    expect(marcadas).toEqual(['9'])
  })

  it('os leads que uma importação deixou com a pessoa abrem a lista de leads', async () => {
    comSessao(atendente)
    comQuadro()
    comLista([leadResumo()])
    comNotificacoes([
      notificacao({
        tipo: 'leads_importados',
        titulo: 'Você recebeu 3 leads da importação',
        lead: null,
      }),
    ])
    const { usuario, router } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Notificações: 1 não lida' }))
    await usuario.click(
      await screen.findByRole('menuitem', { name: /Você recebeu 3 leads da importação/ }),
    )

    await waitFor(() => expect(router.state.location.pathname).toBe('/leads'))
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
