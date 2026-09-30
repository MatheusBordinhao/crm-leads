import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { admin, atendente, comSessao } from '@/test/api'
import { comQuadro } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

describe('layout', () => {
  it('atendente não vê Usuários no menu', async () => {
    comSessao(atendente)
    comQuadro()

    renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByRole('link', { name: 'Funil' })).toHaveAttribute('href', '/funil')
    expect(within(menu).getByRole('link', { name: 'Leads' })).toHaveAttribute('href', '/leads')
    expect(within(menu).queryByRole('link', { name: 'Usuários' })).not.toBeInTheDocument()
  })

  it('admin vê Usuários em Configurações', async () => {
    comSessao(admin)
    comQuadro()

    renderizarApp('/')

    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByText('Configurações')).toBeInTheDocument()
    expect(within(menu).getByRole('link', { name: 'Usuários' })).toHaveAttribute(
      'href',
      '/configuracoes/usuarios',
    )
  })

  it('no celular, o menu abre numa gaveta', async () => {
    comSessao(admin)
    comQuadro()
    const { usuario } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Abrir o menu' }))

    const gaveta = await screen.findByRole('dialog')
    expect(within(gaveta).getByRole('link', { name: 'Usuários' })).toBeInTheDocument()
  })

  it('Sair encerra a sessão e volta ao login', async () => {
    comSessao(atendente)
    comQuadro()
    let saiu = false
    servidor.use(
      http.post('*/api/auth/logout', () => {
        saiu = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { usuario, router } = renderizarApp('/')

    await usuario.click(await screen.findByRole('button', { name: 'Adriana' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Sair' }))

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(saiu).toBe(true)
  })
})
