import { Link } from 'react-router'
import { Button } from '@/components/ui/button'

export function NaoEncontradaPage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 p-4 text-center">
      <h1 className="text-xl font-semibold">Página não encontrada</h1>
      <p className="text-muted-foreground">O endereço pode ter mudado ou não existir mais.</p>
      <Button asChild>
        <Link to="/">Voltar ao início</Link>
      </Button>
    </main>
  )
}
