import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import { criarQueryClient } from '@/lib/queryClient'
import { Provedores } from '@/Provedores'
import { rotas } from '@/rotas'
import { criarHubFalso } from './hubFalso'
import { comAsTelasCarregadas } from './telas'

// As telas são carregadas sob demanda (`tela()`, em rotas.tsx). Nos testes do app, o roteador recebe as telas já
// carregadas, como quando o rotas.tsx as importava direto: cada tela aparece sem a espera do lazy, e os findBy esperam
// o mesmo que antes. O caminho sob demanda (o lazy, o Carregando e o ErroDaTela) é testado no rotas.test.tsx. Fica
// aqui, e não no setup.ts, para os arquivos que não renderizam o app não carregarem as telas à toa.
const rotasComAsTelasCarregadas = await comAsTelasCarregadas(rotas)

/**
 * Renderiza a aplicação inteira, com as rotas de verdade, começando em `caminho`. O hub do tempo real é falso: o teste
 * emite os avisos pelo `hub` devolvido. As `rotasDoApp` trocam as rotas, para um teste montar uma tela sob demanda.
 */
export function renderizarApp(
  caminho = '/',
  rotasDoApp: RouteObject[] = rotasComAsTelasCarregadas,
) {
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
