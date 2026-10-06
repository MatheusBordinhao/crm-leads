import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useEffectEvent } from 'react'
import { toast } from 'sonner'
import { api } from '@/api/cliente'
import { chaveNotificacoes, consultaNotificacoes } from '@/features/notificacoes/dados'
import { useAbrirNotificacao } from '@/features/notificacoes/useAbrirNotificacao'
import { chaveEu } from '@/lib/queryClient'
import {
  devePerguntar,
  lembrarResposta,
  mostrarNotificacao,
  pedirPermissao,
  podeMostrar,
} from './avisosNavegador'
import { useCriarConexao } from './contexto'
import { criarRecarga } from './recarga'

/** Esperas antes de cada nova tentativa de conexão. A última se repete, sem desistir. */
const ESPERAS_RECONEXAO = [0, 2_000, 10_000, 30_000]

/** Uma conexão que durou isto estava boa: se cair, a próxima tentativa volta às esperas curtas. */
const CONEXAO_ESTAVEL = 30_000

/** A Web Lock que impede o Chrome e o Edge de congelarem a aba enquanto ela está conectada. */
const TRAVA_DA_ABA = 'crm-tempo-real'

/** Só um 401 quer dizer sessão encerrada. Um erro de rede (a API reiniciando, por exemplo) não diz nada sobre ela. */
async function sessaoEncerrada() {
  try {
    const { response } = await api.GET('/api/auth/eu')
    return response.status === 401
  } catch {
    return false
  }
}

/**
 * Mantém a tela em dia com o hub do tempo real (seção 3.6). Fica no Layout, que só existe com sessão e senha
 * definitiva: conecta ao montar e desconecta ao sair. Cada aviso recarrega o que mudou (criarRecarga).
 *
 * A reconexão é daqui, e não do SignalR, que não tenta de novo quando o primeiro início falha nem quando o servidor
 * encerra a conexão: as tentativas vêm depois de 0, 2, 10 e 30 s e, daí em diante, a cada 30 s. Antes de cada uma, a
 * sessão é conferida; encerrada, a tela vai para o login. Ao reconectar, tudo é recarregado, porque os avisos do tempo
 * sem conexão se perderam.
 *
 * Com a aba escondida e a permissão dada, uma notificação nova também aparece como notificação nativa do navegador,
 * só com o título. A permissão é pedida uma vez, num aviso com "Ativar" e "Agora não".
 */
export function TempoReal() {
  const criarConexao = useCriarConexao()
  const queryClient = useQueryClient()
  const abrir = useAbrirNotificacao()

  // Busca a lista, que também atualiza o sino, e mostra o título da notificação nova.
  const avisarNoNavegador = useEffectEvent(async (id: number) => {
    const buscar = async () =>
      (await queryClient.fetchQuery(consultaNotificacoes)).itens.find((n) => n.id === id)
    try {
      // Dois avisos seguidos: a primeira busca pode ter saído antes da segunda notificação.
      const notificacao = (await buscar()) ?? (await buscar())
      if (notificacao) {
        mostrarNotificacao(notificacao.titulo, id, () => abrir(notificacao))
      }
    } catch {
      void queryClient.invalidateQueries({ queryKey: chaveNotificacoes })
    }
  })

  useEffect(() => {
    if (devePerguntar()) {
      toast('Avisos do navegador', {
        id: 'pedido-de-avisos',
        description: 'Saiba de um lead novo mesmo com o CRM em segundo plano.',
        duration: Infinity,
        action: { label: 'Ativar', onClick: () => void pedirPermissao() },
        cancel: { label: 'Agora não', onClick: lembrarResposta },
      })
    }
  }, [])

  useEffect(() => {
    const conexao = criarConexao()
    const recarga = criarRecarga(queryClient)
    conexao.ao('LeadsAlterados', (ids) => recarga.leads(ids))
    conexao.ao('NotificacaoNova', (id) => {
      // Com a notificação nativa, a busca da lista já atualiza o sino; sem ela, o sino recarrega como o resto.
      if (podeMostrar()) {
        void avisarNoNavegador(id)
      } else {
        recarga.notificacoes()
      }
    })
    conexao.ao('FunilAlterado', () => recarga.funil())

    let ativa = true
    let tentativas = 0
    let conectadaEm: number | undefined
    let espera: ReturnType<typeof setTimeout> | undefined

    const conectar = async () => {
      const reconexao = tentativas > 0
      if (reconexao && (await sessaoEncerrada())) {
        // Sem sessão, a RotaProtegida leva ao login, e o Layout, com esta conexão, sai da tela.
        if (ativa) {
          queryClient.setQueryData(chaveEu, null)
        }
        return
      }
      if (!ativa) {
        return
      }
      try {
        await conexao.iniciar()
      } catch {
        agendar()
        return
      }
      if (!ativa) {
        void conexao.parar()
        return
      }
      conectadaEm = Date.now()
      if (reconexao) {
        recarga.tudo()
      }
    }

    const agendar = () => {
      if (ativa) {
        espera = setTimeout(
          conectar,
          ESPERAS_RECONEXAO[Math.min(tentativas, ESPERAS_RECONEXAO.length - 1)],
        )
        tentativas++
      }
    }

    conexao.aoFechar(() => {
      // Uma conexão que o servidor recusa logo depois de abrir não volta às esperas curtas.
      if (conectadaEm !== undefined && Date.now() - conectadaEm >= CONEXAO_ESTAVEL) {
        tentativas = 0
      }
      conectadaEm = undefined
      agendar()
    })

    // Começa depois do render: no StrictMode do desenvolvimento, a primeira montagem sai antes de conectar.
    espera = setTimeout(conectar, 0)

    const trava = new AbortController()
    if ('locks' in navigator) {
      navigator.locks
        .request(
          TRAVA_DA_ABA,
          { mode: 'shared', signal: trava.signal },
          () =>
            new Promise<void>((liberar) => trava.signal.addEventListener('abort', () => liberar())),
        )
        .catch(() => {})
    }

    return () => {
      ativa = false
      clearTimeout(espera)
      recarga.cancelar()
      trava.abort()
      void conexao.parar()
    }
  }, [criarConexao, queryClient])

  return null
}
