import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, exigirSucesso, type FiltrosPainel } from '@/api/cliente'

/** O painel, que o tempo real recarrega quando um lead ou o funil muda. */
export const chavePainel = ['painel'] as const

export function usePainel(filtros: FiltrosPainel) {
  return useQuery({
    queryKey: [...chavePainel, filtros],
    queryFn: () => exigirSucesso(api.GET('/api/painel', { params: { query: filtros } })),
    // Mantém os números na tela enquanto um filtro novo carrega.
    placeholderData: keepPreviousData,
  })
}
