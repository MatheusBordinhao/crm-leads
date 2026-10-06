import { formatarNumero } from '@/lib/numeros'
import { cn } from '@/lib/utils'

export type ItemBarra = {
  chave: string | number
  rotulo: string
  valor: number
  /** Texto menor embaixo do nome, como os ganhos e a conversão. */
  detalhe?: string
}

/**
 * Barras horizontais de uma lista curta. Cada linha tem o nome e o número em texto, que o leitor de tela lê, e a
 * barra, proporcional ao maior valor da lista, que é só para o olho. Os espaços entre os textos não aparecem na tela
 * (são itens de um flex), mas separam as palavras para o leitor de tela.
 */
export function Barras({ itens, rotuloId }: { itens: ItemBarra[]; rotuloId: string }) {
  const maior = Math.max(1, ...itens.map((item) => item.valor))
  return (
    <ul aria-labelledby={rotuloId} className="flex flex-col gap-3">
      {itens.map((item) => (
        <li key={item.chave} className="flex flex-col gap-1">
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="min-w-0 break-words">{item.rotulo}</span>{' '}
            <span className="shrink-0 font-medium tabular-nums">{formatarNumero(item.valor)}</span>
          </div>
          {item.detalhe && (
            <>
              {' '}
              <span className="text-xs text-muted-foreground">{item.detalhe}</span>
            </>
          )}
          <div
            aria-hidden
            className={cn('h-2 rounded-r-[4px] bg-grafico', item.valor > 0 && 'min-w-1')}
            style={{ width: `${(item.valor / maior) * 100}%` }}
          />
        </li>
      ))}
    </ul>
  )
}
