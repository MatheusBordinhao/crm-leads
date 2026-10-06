import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  exigirSucesso,
  type CartaoFunilResponse,
  type FiltrosFunil,
  type QuadroFunilResponse,
} from '@/api/cliente'
import {
  aposMudarEtapa,
  chaveFunil,
  chaveMudarEtapa,
  mudarEtapa,
  type MudancaDeEtapa,
} from '@/features/leads/dados'

export function useQuadro(filtros: FiltrosFunil) {
  return useQuery({
    queryKey: [...chaveFunil, filtros],
    queryFn: () => exigirSucesso(api.GET('/api/funil', { params: { query: filtros } })),
    // Mantém o quadro na tela enquanto um filtro novo carrega.
    placeholderData: keepPreviousData,
  })
}

/** O cartão que sai da coluna de origem, para a atualização otimista. */
export type MovimentoNoQuadro = MudancaDeEtapa & { cartao: CartaoFunilResponse; origemId: number }

/**
 * O quadro com o cartão na etapa nova: sai da coluna de origem e entra na de destino, na ordem de entrada;
 * em Ganho e Perdido, soma no contador.
 */
export function moverCartao(
  quadro: QuadroFunilResponse,
  { cartao, origemId, dados }: MovimentoNoQuadro,
): QuadroFunilResponse {
  const colunas = quadro.colunas.map((coluna) => {
    if (coluna.etapa.id === origemId) {
      return {
        ...coluna,
        total: coluna.total - 1,
        leads: coluna.leads.filter((lead) => lead.id !== cartao.id),
      }
    }
    if (coluna.etapa.id === dados.etapaId) {
      const leads = [...coluna.leads, cartao].sort(
        (a, b) => b.criadoEm.localeCompare(a.criadoEm) || b.id - a.id,
      )
      return { ...coluna, total: coluna.total + 1, leads }
    }
    return coluna
  })
  const somar = (area: QuadroFunilResponse['ganho']) =>
    area.etapa.id === dados.etapaId ? { ...area, periodo: area.periodo + 1 } : area
  return { ...quadro, colunas, ganho: somar(quadro.ganho), perdido: somar(quadro.perdido) }
}

/**
 * Move o cartão na hora, antes da resposta da API, e desfaz se ela recusar (um Ganho sem documento, por
 * exemplo). Depois, a ficha, a lista e o quadro são recarregados; o quadro também depois de uma recusa, para
 * trazer o que o tempo real avisou enquanto a mudança estava a caminho.
 */
export function useMoverNoQuadro(filtros: FiltrosFunil) {
  const queryClient = useQueryClient()
  const chave = [...chaveFunil, filtros]
  return useMutation({
    mutationKey: chaveMudarEtapa,
    mutationFn: (movimento: MovimentoNoQuadro) => mudarEtapa(movimento),
    onMutate: async (movimento) => {
      await queryClient.cancelQueries({ queryKey: chave })
      const anterior = queryClient.getQueryData<QuadroFunilResponse>(chave)
      if (anterior) {
        queryClient.setQueryData(chave, moverCartao(anterior, movimento))
      }
      return { anterior }
    },
    onError: (_erro, _movimento, contexto) => {
      if (contexto?.anterior) {
        queryClient.setQueryData(chave, contexto.anterior)
      }
      return queryClient.invalidateQueries({ queryKey: chaveFunil })
    },
    onSuccess: (lead) => aposMudarEtapa(queryClient, lead),
  })
}
