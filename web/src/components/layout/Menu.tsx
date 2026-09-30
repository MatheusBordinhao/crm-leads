import {
  ContactRoundIcon,
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

/** Itens do menu. Cada passo acrescenta as telas que entrega. */
function gruposDoMenu(perfil: Perfil): Grupo[] {
  const grupos: Grupo[] = [
    {
      itens: [
        { para: '/funil', rotulo: 'Funil', icone: SquareKanbanIcon },
        { para: '/leads', rotulo: 'Leads', icone: ContactRoundIcon },
      ],
    },
  ]
  if (perfil === 'Admin') {
    grupos.push({
      titulo: 'Configurações',
      itens: [
        { para: '/configuracoes/usuarios', rotulo: 'Usuários', icone: UsersIcon },
        { para: '/configuracoes/funil', rotulo: 'Etapas e motivos', icone: SlidersHorizontalIcon },
      ],
    })
  }
  return grupos
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
