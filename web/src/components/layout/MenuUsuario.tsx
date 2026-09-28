import { ChevronDownIcon, KeyRoundIcon, LogOutIcon } from 'lucide-react'
import { useNavigate } from 'react-router'
import type { EuResponse } from '@/api/cliente'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useLogout } from '@/features/acesso/sessao'

export function MenuUsuario({ eu }: { eu: EuResponse }) {
  const navigate = useNavigate()
  const logout = useLogout()

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" className="gap-1">
          <span className="max-w-40 truncate">{eu.nome}</span>
          <ChevronDownIcon className="size-4" aria-hidden />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-56">
        <DropdownMenuLabel className="flex flex-col">
          <span className="truncate">{eu.email}</span>
          <span className="text-xs font-normal text-muted-foreground">{eu.perfil}</span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => navigate('/trocar-senha')}>
          <KeyRoundIcon aria-hidden />
          Trocar senha
        </DropdownMenuItem>
        <DropdownMenuItem
          onSelect={() => logout.mutate(undefined, { onSettled: () => navigate('/login') })}
        >
          <LogOutIcon aria-hidden />
          Sair
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
