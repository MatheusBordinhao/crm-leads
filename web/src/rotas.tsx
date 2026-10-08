import type { RouteObject } from 'react-router'
import { Layout } from '@/components/layout/Layout'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { LoginPage } from '@/features/acesso/LoginPage'
import { RotaAdmin, RotaProtegida, TelaInicial } from '@/features/acesso/RotasProtegidas'
import { TrocarSenhaPage } from '@/features/acesso/TrocarSenhaPage'
import { ConfiguracaoFunilPage } from '@/features/configuracoes/ConfiguracaoFunilPage'
import { FunilPage } from '@/features/funil/FunilPage'
import { ImportacaoPage } from '@/features/importacao/ImportacaoPage'
import { ResultadoImportacaoPage } from '@/features/importacao/ResultadoImportacaoPage'
import { LeadPage } from '@/features/leads/LeadPage'
import { LeadsPage } from '@/features/leads/LeadsPage'
import { NovoLeadPage } from '@/features/leads/NovoLeadPage'
import { PainelPage } from '@/features/painel/PainelPage'
import { RelatorioPage } from '@/features/relatorio/RelatorioPage'
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
          { path: '/', element: <TelaInicial /> },
          { path: '/painel', element: <PainelPage /> },
          { path: '/funil', element: <FunilPage /> },
          { path: '/leads', element: <LeadsPage /> },
          { path: '/leads/novo', element: <NovoLeadPage /> },
          { path: '/leads/:id', element: <LeadPage /> },
          {
            element: <RotaAdmin />,
            children: [
              { path: '/relatorio', element: <RelatorioPage /> },
              { path: '/importacao', element: <ImportacaoPage /> },
              { path: '/importacao/:id', element: <ResultadoImportacaoPage /> },
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
