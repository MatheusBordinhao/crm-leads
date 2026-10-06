import type { AvisosTempoReal, ConexaoTempoReal, CriarConexao } from '@/features/tempo-real/conexao'

/**
 * Hub do tempo real falso, para os testes: nenhuma requisição vai para o MSW. O teste emite avisos como o servidor,
 * fecha a conexão como uma queda e faz os próximos inícios falharem, como com a API fora do ar.
 */
export function criarHubFalso() {
  const tratadores: Partial<AvisosTempoReal> = {}
  let aoFechar: (() => void) | undefined
  const estado = { inicios: 0, paradas: 0, conectada: false, falharInicio: false }

  const criar: CriarConexao = (): ConexaoTempoReal => ({
    ao: (aviso, tratar) => {
      tratadores[aviso] = tratar
    },
    aoFechar: (tratar) => {
      aoFechar = tratar
    },
    iniciar: async () => {
      estado.inicios++
      if (estado.falharInicio) {
        throw new Error('A API está fora do ar.')
      }
      estado.conectada = true
    },
    parar: async () => {
      estado.paradas++
      estado.conectada = false
    },
  })

  return {
    criar,
    estado,
    /** Um aviso do servidor, como o hub manda. */
    emitir<K extends keyof AvisosTempoReal>(
      aviso: K,
      ...argumentos: Parameters<AvisosTempoReal[K]>
    ) {
      const tratar = tratadores[aviso] as
        ((...valores: Parameters<AvisosTempoReal[K]>) => void) | undefined
      tratar?.(...argumentos)
    },
    /** A conexão cai, ou o servidor a encerra. */
    fechar() {
      estado.conectada = false
      aoFechar?.()
    },
  }
}

export type HubFalso = ReturnType<typeof criarHubFalso>
