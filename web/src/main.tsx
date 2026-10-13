import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import './index.css'
import { rotas } from './rotas.tsx'
import { criarQueryClient } from './lib/queryClient.ts'
import { Provedores } from './Provedores.tsx'
import { registrarServiceWorker } from './pwa/registro.ts'

const router = createBrowserRouter(rotas)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Provedores queryClient={criarQueryClient()}>
      <RouterProvider router={router} />
    </Provedores>
  </StrictMode>,
)

// Só no build de produção: em desenvolvimento, nenhum service worker fica registrado.
void registrarServiceWorker()
