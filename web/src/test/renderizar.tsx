import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { criarQueryClient } from '@/lib/queryClient'
import { Provedores } from '@/Provedores'
import { rotas } from '@/rotas'

/** Renderiza a aplicação inteira, com as rotas de verdade, começando em `caminho`. */
export function renderizarApp(caminho = '/') {
  const queryClient = criarQueryClient()
  const router = createMemoryRouter(rotas, { initialEntries: [caminho] })
  const usuario = userEvent.setup()

  render(
    <Provedores queryClient={queryClient}>
      <RouterProvider router={router} />
    </Provedores>,
  )

  return { router, queryClient, usuario }
}
