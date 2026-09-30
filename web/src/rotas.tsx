import { Navigate, type RouteObject } from 'react-router'
import { Layout } from '@/components/layout/Layout'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { LoginPage } from '@/features/acesso/LoginPage'
import { RotaAdmin, RotaProtegida } from '@/features/acesso/RotasProtegidas'
import { TrocarSenhaPage } from '@/features/acesso/TrocarSenhaPage'
import { ConfiguracaoFunilPage } from '@/features/configuracoes/ConfiguracaoFunilPage'
import { FunilPage } from '@/features/funil/FunilPage'
import { LeadPage } from '@/features/leads/LeadPage'
import { LeadsPage } from '@/features/leads/LeadsPage'
import { NovoLeadPage } from '@/features/leads/NovoLeadPage'
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
          // O funil é a tela inicial, até o painel chegar (Passo 8).
          { path: '/', element: <Navigate to="/funil" replace /> },
          { path: '/funil', element: <FunilPage /> },
          { path: '/leads', element: <LeadsPage /> },
          { path: '/leads/novo', element: <NovoLeadPage /> },
          { path: '/leads/:id', element: <LeadPage /> },
          {
            element: <RotaAdmin />,
            children: [
              { path: '/configuracoes/usuarios', element: <UsuariosPage /> },
              { path: '/configuracoes/funil', element: <ConfiguracaoFunilPage /> },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NaoEncontradaPage /> },
]
