import {
  ArrowRightLeftIcon,
  InboxIcon,
  MessageSquareTextIcon,
  UserRoundIcon,
  type LucideIcon,
} from 'lucide-react'
import type { ItemLinhaDoTempo } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Button } from '@/components/ui/button'
import { formatarDataHora } from '@/lib/datas'
import { useLinhaDoTempo } from './dados'
import { INTERACAO_COM_ARTIGO, ROTULOS_ORIGEM } from './rotulos'

type Descricao = { icone: LucideIcon; titulo: string; detalhe?: string | null }

/** O texto de cada item, com quem fez. Sem usuário, quem fez foi o sistema (uma entrada da integração, por exemplo). */
function descrever(item: ItemLinhaDoTempo): Descricao {
  const quem = item.usuario?.nome
  switch (item.tipo) {
    case 'entrada': {
      const entrada = item.entrada!
      const campanha = entrada.campanha ? ` · ${entrada.campanha.nome}` : ''
      const verbo = entrada.situacao === 'vinculado' ? 'Voltou a entrar' : 'Entrou'
      return {
        icone: InboxIcon,
        titulo: `${verbo} por ${ROTULOS_ORIGEM[entrada.origem]}${campanha}${entrada.teste ? ' (teste)' : ''}`,
      }
    }
    case 'interacao': {
      const interacao = item.interacao!
      return {
        icone: MessageSquareTextIcon,
        titulo: `${quem ?? 'Alguém'} registrou ${INTERACAO_COM_ARTIGO[interacao.tipo]}`,
        detalhe: interacao.descricao,
      }
    }
    case 'etapa': {
      const mudanca = item.etapa!
      const motivo = mudanca.motivoPerda
        ? `Motivo: ${mudanca.motivoPerda.nome}${mudanca.motivoPerdaDetalhe ? ` · ${mudanca.motivoPerdaDetalhe}` : ''}`
        : null
      return {
        icone: ArrowRightLeftIcon,
        titulo: `${quem ?? 'O sistema'} moveu de ${mudanca.anterior.nome} para ${mudanca.nova.nome}`,
        detalhe: motivo,
      }
    }
    case 'responsavel': {
      const { anterior, nova } = item.responsavel!
      const acao = !anterior
        ? `passou o lead para ${nova?.nome}`
        : !nova
          ? `tirou o lead de ${anterior.nome}`
          : `passou o lead de ${anterior.nome} para ${nova.nome}`
      return { icone: UserRoundIcon, titulo: `${quem ?? 'O sistema'} ${acao}` }
    }
  }
}

/** Entradas, interações, mudanças de etapa e trocas de responsável, da mais recente para a mais antiga. */
export function LinhaDoTempo({ leadId }: { leadId: number }) {
  const { data: itens, isPending, isError, refetch } = useLinhaDoTempo(leadId)

  if (isPending) {
    return <Carregando />
  }
  if (isError) {
    return (
      <div className="flex flex-col items-start gap-2 text-sm">
        <p>Não foi possível carregar a linha do tempo.</p>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }
  if (itens.length === 0) {
    return <p className="text-sm text-muted-foreground">Nada registrado ainda.</p>
  }

  return (
    <ol aria-label="Linha do tempo" className="flex flex-col gap-4">
      {itens.map((item, indice) => {
        const { icone: Icone, titulo, detalhe } = descrever(item)
        return (
          <li key={`${item.tipo}-${item.ocorridoEm}-${indice}`} className="flex gap-3">
            <Icone className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
            <div className="flex min-w-0 flex-1 flex-col gap-0.5">
              <p className="text-sm break-words">{titulo}</p>
              {detalhe && (
                <p className="text-sm break-words whitespace-pre-wrap text-muted-foreground">
                  {detalhe}
                </p>
              )}
              <time dateTime={item.ocorridoEm} className="text-xs text-muted-foreground">
                {formatarDataHora(item.ocorridoEm)}
              </time>
            </div>
          </li>
        )
      })}
    </ol>
  )
}
