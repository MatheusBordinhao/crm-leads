import type { RouteObject } from 'react-router'
import { Layout } from '@/components/layout/Layout'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { LoginPage } from '@/features/acesso/LoginPage'
import { RotaProtegida } from '@/features/acesso/RotasProtegidas'
import { TrocarSenhaPage } from '@/features/acesso/TrocarSenhaPage'
import { InicioPage } from '@/features/inicio/InicioPage'

/** Rotas da aplicação. O main.tsx usa um roteador do navegador, e os testes, um em memória. */
export const rotas: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  {
    element: <RotaProtegida />,
    children: [
      { path: '/trocar-senha', element: <TrocarSenhaPage /> },
      {
        element: <Layout />,
        children: [{ path: '/', element: <InicioPage /> }],
      },
    ],
  },
  { path: '*', element: <NaoEncontradaPage /> },
]
