import {
  ChartColumnIcon,
  ContactRoundIcon,
  FileUpIcon,
  SlidersHorizontalIcon,
  SquareKanbanIcon,
  UsersIcon,
  type LucideIcon,
} from 'lucide-react'
import { NavLink } from 'react-router'
import type { Perfil } from '@/api/cliente'
import { cn } from '@/lib/utils'

type Item = { para: string; rotulo: string; icone: LucideIcon }
type Grupo = { titulo?: string; itens: Item[] }

const PAINEL: Item = { para: '/painel', rotulo: 'Painel', icone: ChartColumnIcon }
const FUNIL: Item = { para: '/funil', rotulo: 'Funil', icone: SquareKanbanIcon }
const LEADS: Item = { para: '/leads', rotulo: 'Leads', icone: ContactRoundIcon }

/** Itens do menu. Cada passo acrescenta as telas que entrega, e o primeiro item é a tela inicial do perfil. */
function gruposDoMenu(perfil: Perfil): Grupo[] {
  if (perfil !== 'Admin') {
    // A atendente começa pelo funil, onde ela trabalha.
    return [{ itens: [FUNIL, LEADS, PAINEL] }]
  }
  return [
    {
      itens: [
        PAINEL,
        FUNIL,
        LEADS,
        { para: '/importacao', rotulo: 'Importação', icone: FileUpIcon },
      ],
    },
    {
      titulo: 'Configurações',
      itens: [
        { para: '/configuracoes/usuarios', rotulo: 'Usuários', icone: UsersIcon },
        { para: '/configuracoes/funil', rotulo: 'Etapas e motivos', icone: SlidersHorizontalIcon },
      ],
    },
  ]
}

/** Menu principal. No celular, fica dentro da gaveta e a fecha ao navegar (`aoNavegar`). */
export function Menu({ perfil, aoNavegar }: { perfil: Perfil; aoNavegar?: () => void }) {
  return (
    <nav aria-label="Menu principal" className="flex flex-col gap-4 p-3">
      {gruposDoMenu(perfil).map((grupo, indice) => (
        <div key={grupo.titulo ?? indice} className="flex flex-col gap-1">
          {grupo.titulo && (
            <span className="px-2 text-xs font-medium text-muted-foreground">{grupo.titulo}</span>
          )}
          {grupo.itens.map(({ para, rotulo, icone: Icone }) => (
            <NavLink
              key={para}
              to={para}
              end
              onClick={aoNavegar}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-2 rounded-md px-2 py-2 text-sm hover:bg-accent',
                  isActive && 'bg-accent font-medium',
                )
              }
            >
              <Icone className="size-4" aria-hidden />
              {rotulo}
            </NavLink>
          ))}
        </div>
      ))}
    </nav>
  )
}
