import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { criarQueryClient } from '@/lib/queryClient'
import { Provedores } from '@/Provedores'
import { rotas } from '@/rotas'
import { criarHubFalso } from './hubFalso'

/**
 * Renderiza a aplicação inteira, com as rotas de verdade, começando em `caminho`. O hub do tempo real é falso: o teste
 * emite os avisos pelo `hub` devolvido.
 */
export function renderizarApp(caminho = '/') {
  const queryClient = criarQueryClient()
  const router = createMemoryRouter(rotas, { initialEntries: [caminho] })
  const usuario = userEvent.setup()
  const hub = criarHubFalso()

  render(
    <Provedores queryClient={queryClient} criarConexao={hub.criar}>
      <RouterProvider router={router} />
    </Provedores>,
  )

  return { router, queryClient, usuario, hub }
}
