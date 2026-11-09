import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { criarQueryClient } from '@/lib/queryClient'
import { Provedores } from '@/Provedores'
import { rotas } from '@/rotas'
import { criarHubFalso } from './hubFalso'
import { carregarTelas } from './telas'

// As telas são carregadas sob demanda (`tela()`, em rotas.tsx). Antes dos testes, o arquivo de cada uma já fica em
// cache, como quando o rotas.tsx as importava direto: sem isso, a primeira visita a uma tela pagaria o carregamento
// dentro da espera de um findBy. Fica aqui, e não no setup.ts, para os arquivos que não renderizam o app não
// carregarem as telas à toa.
await carregarTelas(rotas)

/**
 * Renderiza a aplicação inteira, com as rotas de verdade, começando em `caminho`. O hub do tempo real é falso: o teste
 * emite os avisos pelo `hub` devolvido. As `rotasDoApp` trocam as rotas, para um teste montar uma tela que falha.
 */
export function renderizarApp(caminho = '/', rotasDoApp: RouteObject[] = rotas) {
  const queryClient = criarQueryClient()
  const router = createMemoryRouter(rotasDoApp, { initialEntries: [caminho] })
  const usuario = userEvent.setup()
  const hub = criarHubFalso()

  render(
    <Provedores queryClient={queryClient} criarConexao={hub.criar}>
      <RouterProvider router={router} />
    </Provedores>,
  )

  return { router, queryClient, usuario, hub }
}
