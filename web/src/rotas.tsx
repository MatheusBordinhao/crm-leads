import type { ComponentType } from 'react'
import type { RouteObject } from 'react-router'
import { Carregando } from '@/components/Carregando'
import { ErroDaTela } from '@/components/ErroDaTela'
import { Layout } from '@/components/layout/Layout'
import { NaoEncontradaPage } from '@/components/NaoEncontradaPage'
import { RotaAdmin, RotaProtegida, TelaInicial } from '@/features/acesso/RotasProtegidas'
import { ErroAoCarregarTela, marcarRecarga, podeRecarregarSozinha } from '@/lib/recarga'

/**
 * Uma tela carregada sob demanda: o arquivo dela só é baixado quando ela abre pela primeira vez (Passo 11, Lote 4B).
 * - O `lazy` fica na forma de função. Na forma de objeto (`lazy: { Component }`), numa rota sem loader, o React Router
 *   8.4 engole a falha do import: a rota fica sem componente, e a tela, em branco, sem passar pelo ErrorBoundary.
 * - Na primeira carga da página, o HydrateFallback mostra o Carregando no lugar da tela, com o layout em volta.
 * - Se o arquivo não carrega, o ErroDaTela cuida: a página recarrega sozinha uma vez, já na versão nova, ou mostra o
 *   erro com o botão de recarregar (lib/recarga.ts).
 */
export function tela(carregar: () => Promise<ComponentType>) {
  return {
    lazy: async () => {
      try {
        return { Component: await carregar() }
      } catch (causa) {
        // A decisão de recarregar sai aqui, uma vez por falha (o roteador guarda a promessa do lazy), e não na
        // renderização do ErroDaTela: sem recarga no último minuto nesta aba, e só com a marca gravada.
        throw new ErroAoCarregarTela(causa, podeRecarregarSozinha() && marcarRecarga())
      }
    },
    HydrateFallback: Carregando,
    ErrorBoundary: ErroDaTela,
  } satisfies RouteObject
}

/**
 * Rotas da aplicação. O main.tsx usa um roteador do navegador, e os testes, um em memória. Toda tela entra pelo
 * `tela()`; ficam no pacote principal só as rotas que envolvem as telas e a página não encontrada.
 */
export const rotas: RouteObject[] = [
  {
    // Sem caminho nem tela: o React Router dá à primeira rota o ErrorBoundary padrão. Um erro que não é de carga,
    // relançado pelo ErroDaTela, sobe até ela, como antes das telas sob demanda, também no login.
    children: [
      {
        path: '/login',
        ...tela(() => import('@/features/acesso/LoginPage').then((m) => m.LoginPage)),
      },
      {
        element: <RotaProtegida />,
        children: [
          {
            path: '/trocar-senha',
            ...tela(() =>
              import('@/features/acesso/TrocarSenhaPage').then((m) => m.TrocarSenhaPage),
            ),
          },
          {
            element: <Layout />,
            children: [
              { path: '/', element: <TelaInicial /> },
              {
                path: '/painel',
                ...tela(() => import('@/features/painel/PainelPage').then((m) => m.PainelPage)),
              },
              {
                path: '/funil',
                ...tela(() => import('@/features/funil/FunilPage').then((m) => m.FunilPage)),
              },
              {
                path: '/leads',
                ...tela(() => import('@/features/leads/LeadsPage').then((m) => m.LeadsPage)),
              },
              {
                path: '/leads/novo',
                ...tela(() => import('@/features/leads/NovoLeadPage').then((m) => m.NovoLeadPage)),
              },
              {
                path: '/leads/:id',
                ...tela(() => import('@/features/leads/LeadPage').then((m) => m.LeadPage)),
              },
              {
                element: <RotaAdmin />,
                children: [
                  {
                    path: '/relatorio',
                    ...tela(() =>
                      import('@/features/relatorio/RelatorioPage').then((m) => m.RelatorioPage),
                    ),
                  },
                  {
                    path: '/importacao',
                    ...tela(() =>
                      import('@/features/importacao/ImportacaoPage').then((m) => m.ImportacaoPage),
                    ),
                  },
                  {
                    path: '/importacao/:id',
                    ...tela(() =>
                      import('@/features/importacao/ResultadoImportacaoPage').then(
                        (m) => m.ResultadoImportacaoPage,
                      ),
                    ),
                  },
                  {
                    path: '/auditoria',
                    ...tela(() =>
                      import('@/features/auditoria/AuditoriaPage').then((m) => m.AuditoriaPage),
                    ),
                  },
                  {
                    path: '/configuracoes/usuarios',
                    ...tela(() =>
                      import('@/features/usuarios/UsuariosPage').then((m) => m.UsuariosPage),
                    ),
                  },
                  {
                    path: '/configuracoes/funil',
                    ...tela(() =>
                      import('@/features/configuracoes/ConfiguracaoFunilPage').then(
                        (m) => m.ConfiguracaoFunilPage,
                      ),
                    ),
                  },
                  {
                    path: '/configuracoes/campanhas',
                    ...tela(() =>
                      import('@/features/configuracoes/CampanhasPage').then((m) => m.CampanhasPage),
                    ),
                  },
                ],
              },
            ],
          },
        ],
      },
      { path: '*', element: <NaoEncontradaPage /> },
    ],
  },
]
