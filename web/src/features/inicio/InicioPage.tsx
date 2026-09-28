import { useEu } from '@/features/acesso/sessao'

export function InicioPage() {
  const { data: eu } = useEu()

  return (
    <section className="flex flex-col gap-2">
      <h1 className="text-xl font-semibold">Olá, {eu?.nome}</h1>
      <p className="text-muted-foreground">O funil de leads chega no Passo 3.</p>
    </section>
  )
}
