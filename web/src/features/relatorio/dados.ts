import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, exigirSucesso, type FiltrosRelatorio } from '@/api/cliente'

/** O relatório por campanha, que o tempo real recarrega quando um lead muda, como o painel. */
export const chaveRelatorio = ['relatorio'] as const

export function useRelatorioCampanhas(filtros: FiltrosRelatorio) {
  return useQuery({
    queryKey: [...chaveRelatorio, filtros],
    queryFn: () =>
      exigirSucesso(api.GET('/api/relatorios/campanhas', { params: { query: filtros } })),
    // Mantém os números na tela enquanto um período novo carrega.
    placeholderData: keepPreviousData,
  })
}

/** O mesmo relatório em CSV, com o período da tela. A sessão vai no cookie, como nas chamadas à API. */
export function linkDoCsv(filtros: FiltrosRelatorio) {
  const params = new URLSearchParams()
  if (filtros.de) {
    params.set('de', filtros.de)
  }
  if (filtros.ate) {
    params.set('ate', filtros.ate)
  }
  const query = params.toString()
  return `/api/relatorios/campanhas/csv${query ? `?${query}` : ''}`
}
