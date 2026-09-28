import { UsersRoundIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

/** Nome do sistema. O ícone ocupa o lugar do logo da empresa, que entra depois. */
export function Marca({ className }: { className?: string }) {
  return (
    <span className={cn('flex items-center gap-2 font-semibold', className)}>
      <span className="flex size-7 items-center justify-center rounded-md bg-primary text-primary-foreground">
        <UsersRoundIcon className="size-4" aria-hidden />
      </span>
      CRM de Leads
    </span>
  )
}
