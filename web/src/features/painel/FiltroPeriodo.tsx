import { Selecao } from '@/components/Selecao'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'
import {
  lerPeriodo,
  MAIOR_DIA,
  MENOR_DIA,
  PERIODOS,
  ROTULOS_PERIODO,
  type Periodo,
} from './periodo'

/** Muda os filtros guardados no endereço. Um valor vazio ou nulo tira o filtro. */
export type MudarEndereco = (mudancas: Record<string, string | null>) => void

/**
 * O período, guardado no endereço como no painel: `periodo` para os atalhos (os últimos 30 dias, que são o padrão, não
 * vão para o endereço) e `de` e `ate` para o personalizado, que começa com o período que está na tela.
 */
export function SelecaoPeriodo({
  id,
  params,
  mudar,
  intervaloNaTela,
}: {
  id: string
  params: URLSearchParams
  mudar: MudarEndereco
  intervaloNaTela: { de: string; ate: string }
}) {
  const mudarPeriodo = (novo: Periodo) =>
    novo === 'personalizado'
      ? mudar({ periodo: null, ...intervaloNaTela })
      : mudar({ periodo: novo === '30d' ? null : novo, de: null, ate: null })

  return (
    <div className="flex min-w-0 flex-1 flex-col gap-1.5">
      <Label htmlFor={id}>Período</Label>
      <Selecao
        id={id}
        value={lerPeriodo(params)}
        onChange={(evento) => mudarPeriodo(evento.target.value as Periodo)}
      >
        {PERIODOS.map((opcao) => (
          <option key={opcao} value={opcao}>
            {ROTULOS_PERIODO[opcao]}
          </option>
        ))}
      </Selecao>
    </div>
  )
}

/** O início e o fim do período personalizado, em dias do horário de Brasília. */
export function DatasDoPeriodo({
  prefixo,
  params,
  mudar,
  className,
}: {
  prefixo: string
  params: URLSearchParams
  mudar: MudarEndereco
  className?: string
}) {
  const data = (nome: 'de' | 'ate', rotulo: string) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`${prefixo}-${nome}`}>{rotulo}</Label>
      <Input
        id={`${prefixo}-${nome}`}
        type="date"
        min={MENOR_DIA}
        max={MAIOR_DIA}
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar({ [nome]: evento.target.value })}
      />
    </div>
  )

  return (
    <div className={cn('grid grid-cols-2 gap-3', className)}>
      {data('de', 'De')}
      {data('ate', 'Até')}
    </div>
  )
}
