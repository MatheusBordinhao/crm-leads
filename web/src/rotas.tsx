import type { RouteObject } from 'react-router'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { InicioPage } from '@/features/inicio/InicioPage'

/** Rotas da aplicação. O main.tsx usa um roteador do navegador, e os testes, um em memória. */
export const rotas: RouteObject[] = [
  { path: '/', element: <InicioPage /> },
  { path: '*', element: <NaoEncontradaPage /> },
]
