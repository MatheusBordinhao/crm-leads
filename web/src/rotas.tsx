import type { RouteObject } from 'react-router'
import { Layout } from '@/components/layout/Layout'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { LoginPage } from '@/features/acesso/LoginPage'
import { RotaAdmin, RotaProtegida } from '@/features/acesso/RotasProtegidas'
import { TrocarSenhaPage } from '@/features/acesso/TrocarSenhaPage'
import { InicioPage } from '@/features/inicio/InicioPage'
import { LeadsPage } from '@/features/leads/LeadsPage'
import { UsuariosPage } from '@/features/usuarios/UsuariosPage'

/** Rotas da aplicação. O main.tsx usa um roteador do navegador, e os testes, um em memória. */
export const rotas: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  {
    element: <RotaProtegida />,
    children: [
      { path: '/trocar-senha', element: <TrocarSenhaPage /> },
      {
        element: <Layout />,
        children: [
          { path: '/', element: <InicioPage /> },
          { path: '/leads', element: <LeadsPage /> },
          {
            element: <RotaAdmin />,
            children: [{ path: '/configuracoes/usuarios', element: <UsuariosPage /> }],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NaoEncontradaPage /> },
]
