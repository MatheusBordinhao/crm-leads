import { Loader2Icon } from 'lucide-react'

export function Carregando({ texto = 'Carregando…' }: { texto?: string }) {
  return (
    <div role="status" className="flex items-center justify-center gap-2 p-8 text-muted-foreground">
      <Loader2Icon className="size-4 animate-spin" aria-hidden />
      {texto}
    </div>
  )
}
