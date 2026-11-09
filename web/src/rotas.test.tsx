import { screen, within } from '@testing-library/react'
import type { ComponentType } from 'react'
import { Link, type RouteObject } from 'react-router'
import { describe, expect, it } from 'vitest'
import { Carregando } from '@/components/Carregando'
import { ErroDaTela } from '@/components/ErroDaTela'
import { Layout } from '@/components/layout/Layout'
import { RotaProtegida } from '@/features/acesso/RotasProtegidas'
import { rotas, tela } from '@/rotas'
import { atendente, comSessao } from '@/test/api'
import { renderizarApp } from '@/test/renderizar'
import { carregarTelas, todasAsRotas } from '@/test/telas'

/** O layout de verdade, com uma tela que abre e outra que vem pelo `tela()`, com o `carregar` do teste. */
function rotasComUmaTelaSobDemanda(carregar: () => Promise<ComponentType>): RouteObject[] {
  return [
    {
      element: <RotaProtegida />,
      children: [
        {
          element: <Layout />,
          children: [
            { path: '/inicio', element: <Link to="/sob-demanda">Abrir a tela</Link> },
            { path: '/sob-demanda', ...tela(carregar) },
          ],
        },
      ],
    },
  ]
}

/** O import que falha, como o de um arquivo que sumiu do servidor depois de uma publicação nova. */
const importQueFalha = () =>
  Promise.reject(new TypeError('Failed to fetch dynamically imported module: /assets/Tela-abc.js'))

describe('rotas', () => {
  it('mostra a página de não encontrada num endereço desconhecido', async () => {
    renderizarApp('/nao-existe')

    expect(
      await screen.findByRole('heading', { name: 'Página não encontrada' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar ao início' })).toHaveAttribute('href', '/')
  })

  it('cada tela sob demanda carrega um componente', async () => {
    const carregadas = await carregarTelas(rotas)

    expect(carregadas.map(({ rota }) => rota.path)).toEqual([
      '/login',
      '/trocar-senha',
      '/painel',
      '/funil',
      '/leads',
      '/leads/novo',
      '/leads/:id',
      '/relatorio',
      '/importacao',
      '/importacao/:id',
      '/auditoria',
      '/configuracoes/usuarios',
      '/configuracoes/funil',
      '/configuracoes/campanhas',
    ])
    for (const { rota, carregada } of carregadas) {
      expect(carregada.Component, rota.path).toEqual(expect.any(Function))
    }
  })

  it('só o início e a página não encontrada ficam no pacote principal: toda outra tela passa pelo tela()', () => {
    const comCaminho = todasAsRotas(rotas).filter((rota) => rota.path !== undefined)

    expect(comCaminho.filter((rota) => !rota.lazy).map((rota) => rota.path)).toEqual(['/', '*'])
    for (const rota of comCaminho.filter((rota) => rota.lazy)) {
      expect(rota.HydrateFallback, rota.path).toBe(Carregando)
      expect(rota.ErrorBoundary, rota.path).toBe(ErroDaTela)
    }
  })

  it('a tela que não carrega mostra o erro com o botão, no endereço dela e com o menu', async () => {
    comSessao(atendente)
    const { usuario, router } = renderizarApp('/inicio', rotasComUmaTelaSobDemanda(importQueFalha))

    await usuario.click(await screen.findByRole('link', { name: 'Abrir a tela' }))

    expect(
      await screen.findByText(
        'Não foi possível abrir esta tela. Confira a conexão e recarregue a página.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Recarregar a página' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/sob-demanda')
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByRole('link', { name: 'Funil' })).toBeInTheDocument()
  })
})
