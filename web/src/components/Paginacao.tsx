import { Button } from '@/components/ui/button'

/** Anterior e Próxima, com a página atual. Some quando tudo cabe numa página. */
export function Paginacao({
  pagina,
  total,
  tamanho,
  irPara,
}: {
  pagina: number
  total: number
  tamanho: number
  irPara: (pagina: number) => void
}) {
  const paginas = Math.max(1, Math.ceil(total / tamanho))
  if (paginas <= 1) {
    return null
  }
  return (
    <nav aria-label="Páginas" className="flex items-center justify-between gap-2">
      <Button variant="outline" disabled={pagina <= 1} onClick={() => irPara(pagina - 1)}>
        Anterior
      </Button>
      <span className="text-sm text-muted-foreground">
        Página {pagina} de {paginas}
      </span>
      <Button variant="outline" disabled={pagina >= paginas} onClick={() => irPara(pagina + 1)}>
        Próxima
      </Button>
    </nav>
  )
}
