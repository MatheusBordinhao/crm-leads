import { BellIcon, CheckCheckIcon } from 'lucide-react'
import { useNavigate } from 'react-router'
import type { NotificacaoResponse } from '@/api/cliente'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { tempoDesde } from '@/lib/datas'
import { cn } from '@/lib/utils'
import { useMarcarLida, useMarcarTodasLidas, useNotificacoes } from './dados'

/**
 * Sino do topo (seção 3.6): o contador de não lidas e as notificações mais recentes. Clicar numa notificação a
 * marca como lida e abre a ficha do lead, o resultado da importação (no resumo, para quem importou) ou a lista de
 * leads (nos leads que uma importação deixou com a pessoa). A lista se atualiza pelo tempo real (TempoReal).
 */
export function Sino() {
  const { data } = useNotificacoes()
  const marcarLida = useMarcarLida()
  const marcarTodas = useMarcarTodasLidas()
  const navigate = useNavigate()
  const naoLidas = data?.naoLidas ?? 0
  const itens = data?.itens ?? []

  const abrir = (notificacao: NotificacaoResponse) => {
    if (!notificacao.lidaEm) {
      marcarLida.mutate(notificacao.id)
    }
    if (notificacao.lead) {
      navigate(`/leads/${notificacao.lead.id}`)
    } else if (notificacao.importacaoId) {
      navigate(`/importacao/${notificacao.importacaoId}`)
    } else if (notificacao.tipo === 'leads_importados') {
      navigate('/leads')
    }
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={
            naoLidas > 0
              ? `Notificações: ${naoLidas} ${naoLidas === 1 ? 'não lida' : 'não lidas'}`
              : 'Notificações'
          }
        >
          <BellIcon />
          {naoLidas > 0 && (
            <span
              aria-hidden
              className="absolute top-0.5 right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] leading-none font-medium text-white"
            >
              {naoLidas > 9 ? '9+' : naoLidas}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80 max-w-[calc(100vw-1rem)]">
        <DropdownMenuLabel>Notificações</DropdownMenuLabel>
        <DropdownMenuItem
          disabled={naoLidas === 0 || marcarTodas.isPending}
          onSelect={(evento) => {
            // Continua aberta, para a pessoa ver as notificações lidas.
            evento.preventDefault()
            marcarTodas.mutate()
          }}
        >
          <CheckCheckIcon aria-hidden />
          Marcar todas como lidas
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        {itens.length === 0 ? (
          <p className="px-2 py-3 text-sm text-muted-foreground">Nenhuma notificação.</p>
        ) : (
          <div className="max-h-[min(70vh,28rem)] overflow-y-auto">
            {itens.map((notificacao) => (
              <ItemNotificacao
                key={notificacao.id}
                notificacao={notificacao}
                aoAbrir={() => abrir(notificacao)}
              />
            ))}
          </div>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

function ItemNotificacao({
  notificacao,
  aoAbrir,
}: {
  notificacao: NotificacaoResponse
  aoAbrir: () => void
}) {
  const naoLida = !notificacao.lidaEm
  const quando = tempoDesde(notificacao.criadoEm)
  // O rótulo junta as partes com pontuação: sem ele, o leitor de tela leria os textos grudados.
  const rotulo = [
    naoLida ? `${notificacao.titulo} (não lida)` : notificacao.titulo,
    notificacao.lead?.nome,
    quando,
  ]
    .filter(Boolean)
    .join(', ')
  return (
    <DropdownMenuItem onSelect={aoAbrir} aria-label={rotulo} className="items-start gap-2">
      <span
        aria-hidden
        className={cn(
          'mt-1.5 size-2 shrink-0 rounded-full',
          naoLida ? 'bg-primary' : 'bg-transparent',
        )}
      />
      <span className="flex min-w-0 flex-col gap-0.5">
        <span className={cn('break-words', naoLida && 'font-medium')}>{notificacao.titulo}</span>
        {notificacao.lead && (
          <span className="truncate text-muted-foreground">{notificacao.lead.nome}</span>
        )}
        <span className="text-xs text-muted-foreground">{quando}</span>
      </span>
    </DropdownMenuItem>
  )
}
