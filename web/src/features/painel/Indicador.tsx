/** Um número em destaque do painel: o nome em cima, o valor e, embaixo, o que ajuda a lê-lo. */
export function Indicador({
  rotulo,
  valor,
  detalhe,
}: {
  rotulo: string
  valor: string
  detalhe?: string
}) {
  return (
    <div
      role="group"
      aria-label={rotulo}
      className="flex min-w-0 flex-col gap-1 rounded-xl bg-card p-4 text-card-foreground ring-1 ring-foreground/10"
    >
      <p className="text-sm text-muted-foreground">{rotulo}</p>
      <p className="text-2xl font-semibold">{valor}</p>
      {detalhe && <p className="text-xs text-muted-foreground">{detalhe}</p>}
    </div>
  )
}
