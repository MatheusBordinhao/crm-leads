import { screen, waitFor, within } from '@testing-library/react'
import type { ComponentType } from 'react'
import { Link, type RouteObject } from 'react-router'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { Carregando } from '@/components/Carregando'
import { ErroDaTela } from '@/components/ErroDaTela'
import { Layout } from '@/components/layout/Layout'
import { RotaProtegida } from '@/features/acesso/RotasProtegidas'
import { CHAVE_RECARGA, recarregarPagina } from '@/lib/recarga'
import { rotas, tela } from '@/rotas'
import { atendente, comSessao } from '@/test/api'
import { renderizarApp } from '@/test/renderizar'
import { carregarTelas, todasAsRotas } from '@/test/telas'

// O jsdom não recarrega a página: os testes conferem a chamada.
vi.mock(import('@/lib/recarga'), async (original) => ({
  ...(await original()),
  recarregarPagina: vi.fn(),
}))

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

const MENSAGEM_DE_ERRO =
  'Não foi possível abrir esta tela. Confira a conexão e recarregue a página.'

/** Abre a tela sob demanda pelo link, como um clique no menu. */
async function abrirATela(carregar: () => Promise<ComponentType>) {
  comSessao(atendente)
  const app = renderizarApp('/inicio', rotasComUmaTelaSobDemanda(carregar))
  await app.usuario.click(await screen.findByRole('link', { name: 'Abrir a tela' }))
  return app
}

describe('rotas', () => {
  beforeEach(() => {
    sessionStorage.clear()
    vi.mocked(recarregarPagina).mockClear()
  })

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

  it('a tela que não carrega recarrega a página uma vez, já no endereço dela, sem mostrar o erro', async () => {
    const { router } = await abrirATela(importQueFalha)

    await waitFor(() => expect(recarregarPagina).toHaveBeenCalledTimes(1))
    expect(router.state.location.pathname).toBe('/sob-demanda')
    expect(within(screen.getByRole('main')).getByText('Carregando…')).toBeInTheDocument()
    expect(screen.queryByText(MENSAGEM_DE_ERRO)).not.toBeInTheDocument()
    expect(sessionStorage.getItem(CHAVE_RECARGA)).not.toBeNull()
  })

  it('com uma recarga há menos de um minuto, mostra o erro com o botão, sem recarregar sozinha', async () => {
    sessionStorage.setItem(CHAVE_RECARGA, String(Date.now()))
    const { usuario, router } = await abrirATela(importQueFalha)

    expect(await screen.findByText(MENSAGEM_DE_ERRO)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/sob-demanda')
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).getByRole('link', { name: 'Funil' })).toBeInTheDocument()
    expect(recarregarPagina).not.toHaveBeenCalled()

    await usuario.click(screen.getByRole('button', { name: 'Recarregar a página' }))

    expect(recarregarPagina).toHaveBeenCalledTimes(1)
  })

  it('um erro que não é de carga não recarrega e segue para o ErrorBoundary padrão, como antes', async () => {
    function TelaComDefeito(): never {
      throw new Error('defeito de teste')
    }
    await abrirATela(async () => TelaComDefeito)

    expect(
      await screen.findByRole('heading', { name: 'Unexpected Application Error!' }),
    ).toBeInTheDocument()
    expect(screen.queryByText(MENSAGEM_DE_ERRO)).not.toBeInTheDocument()
    expect(recarregarPagina).not.toHaveBeenCalled()
  })
})
