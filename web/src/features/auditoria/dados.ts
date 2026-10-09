import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api, exigirSucesso, type FiltrosAuditoria } from '@/api/cliente'

export const chaveAuditoria = ['auditoria'] as const

/** Os registros da auditoria com os filtros da tela. A página anterior fica na tela enquanto a próxima carrega. */
export function useAuditoria(filtros: FiltrosAuditoria) {
  return useQuery({
    queryKey: [...chaveAuditoria, filtros],
    queryFn: () => exigirSucesso(api.GET('/api/auditoria', { params: { query: filtros } })),
    placeholderData: keepPreviousData,
  })
}
