import type { QueryClient } from '@tanstack/react-query'
import {
  chaveEtapas,
  chaveFicha,
  chaveFunil,
  chaveLeads,
  chaveLinhaDoTempo,
  chaveLista,
  chaveMotivosPerda,
  chaveMudarEtapa,
} from '@/features/leads/dados'
import { chaveNotificacoes, consultaNotificacoes } from '@/features/notificacoes/dados'
import { chavePainel } from '@/features/painel/dados'

/** No máximo uma recarga por segundo: uma importação grande manda um aviso atrás do outro. */
export const INTERVALO_RECARGA = 1000

type Pendente = { leads: Set<number>; tudoDeLeads: boolean; notificacoes: boolean; funil: boolean }

const vazio = (): Pendente => ({
  leads: new Set(),
  tudoDeLeads: false,
  notificacoes: false,
  funil: false,
})

/**
 * Junta os avisos do hub e recarrega o que eles mudaram: o primeiro aviso na hora, os seguintes no fim do segundo. Só as
 * consultas que estão na tela vão à API; as outras ficam marcadas para recarregar quando aparecerem.
 */
export function criarRecarga(queryClient: QueryClient) {
  let pendente = vazio()
  let ultima = 0
  let timer: ReturnType<typeof setTimeout> | undefined

  const recarregar = (queryKey: readonly unknown[]) =>
    void queryClient.invalidateQueries({ queryKey })

  const executar = () => {
    timer = undefined
    ultima = Date.now()
    const { leads, tudoDeLeads, notificacoes, funil } = pendente
    pendente = vazio()
    if (funil) {
      recarregar(chaveEtapas)
      recarregar(chaveMotivosPerda)
    }
    // Com uma mudança de etapa a caminho, o quadro já tem o cartão na coluna nova e só volta à API quando ela terminar.
    const mudandoEtapa = queryClient.isMutating({ mutationKey: chaveMudarEtapa }) > 0
    if ((funil || tudoDeLeads || leads.size > 0) && !mudandoEtapa) {
      recarregar(chaveFunil)
    }
    // O painel conta os leads e mostra os nomes das etapas.
    if (funil || tudoDeLeads || leads.size > 0) {
      recarregar(chavePainel)
    }
    if (tudoDeLeads) {
      // A lista, as fichas e as linhas do tempo: um lead da pessoa passou para outra, e o id dele não vem no aviso.
      recarregar(chaveLeads)
    } else if (leads.size > 0) {
      recarregar(chaveLista)
      for (const id of leads) {
        recarregar(chaveFicha(id))
        recarregar(chaveLinhaDoTempo(id))
      }
    }
    // O sino mostra o nome atual do lead de cada notificação e tira a de um lead que passou para outra pessoa.
    const noSino = queryClient
      .getQueryData(consultaNotificacoes.queryKey)
      ?.itens.some((n) => n.lead !== null && leads.has(n.lead.id))
    if (notificacoes || tudoDeLeads || noSino) {
      // Sem cancelar a busca que a notificação nativa já fez (TempoReal), que chegou depois do aviso e está em dia.
      void queryClient.invalidateQueries({ queryKey: chaveNotificacoes }, { cancelRefetch: false })
    }
  }

  const agendar = () => {
    if (timer === undefined) {
      timer = setTimeout(executar, Math.max(0, ultima + INTERVALO_RECARGA - Date.now()))
    }
  }

  return {
    leads(ids: number[]) {
      if (ids.length === 0) {
        pendente.tudoDeLeads = true
      }
      for (const id of ids) {
        pendente.leads.add(id)
      }
      agendar()
    },
    notificacoes() {
      pendente.notificacoes = true
      agendar()
    },
    funil() {
      pendente.funil = true
      agendar()
    },
    /** Depois de reconectar: os avisos do tempo sem conexão se perderam, então tudo é recarregado. */
    tudo() {
      void queryClient.invalidateQueries()
    },
    cancelar() {
      clearTimeout(timer)
    },
  }
}
