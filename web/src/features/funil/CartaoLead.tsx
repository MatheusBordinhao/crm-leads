import type { ReactNode } from 'react'
import { Link } from 'react-router'
import type { CartaoFunilResponse } from '@/api/cliente'
import { Badge } from '@/components/ui/badge'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { formatarDataHora, tempoDesde } from '@/lib/datas'
import { cn } from '@/lib/utils'

type Props = {
  cartao: CartaoFunilResponse
  /** Só o admin vê a responsável: a atendente só tem os próprios leads. */
  mostrarResponsavel: boolean
  /** Ao lado do nome: a alça de arrastar, no quadro. */
  alca?: ReactNode
  /** Embaixo: o "Mover para", na lista do celular. */
  rodape?: ReactNode
  className?: string
}

/** Cartão do funil: nome, empresa, origem, tempo desde a entrada e, para o admin, a responsável. */
export function CartaoLead({ cartao, mostrarResponsavel, alca, rodape, className }: Props) {
  return (
    <div
      className={cn(
        'flex flex-col gap-2 rounded-lg border bg-card p-3 text-sm text-card-foreground shadow-xs',
        className,
      )}
    >
      <div className="flex items-start gap-2">
        <div className="min-w-0 flex-1">
          <Link to={`/leads/${cartao.id}`} className="block truncate font-medium hover:underline">
            {cartao.nome}
          </Link>
          {cartao.empresa && <div className="truncate text-muted-foreground">{cartao.empresa}</div>}
        </div>
        {alca}
      </div>
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
        <span>{ROTULOS_ORIGEM[cartao.origem]}</span>
        <time dateTime={cartao.criadoEm} title={formatarDataHora(cartao.criadoEm)}>
          {tempoDesde(cartao.criadoEm)}
        </time>
        {mostrarResponsavel && <span>{cartao.responsavel?.nome ?? 'Sem responsável'}</span>}
        {cartao.teste && <Badge variant="outline">Teste</Badge>}
      </div>
      {rodape}
    </div>
  )
}
