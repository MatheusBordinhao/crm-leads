import { queryOptions, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, exigirSucesso } from '@/api/cliente'

/** Notificações do usuário logado: o tempo real as recarrega quando chega uma nova. */
export const chaveNotificacoes = ['notificacoes'] as const

/**
 * As notificações mais recentes do usuário logado e quantas não foram lidas. O tempo real também a usa para buscar o
 * título da notificação nativa.
 */
export const consultaNotificacoes = queryOptions({
  queryKey: chaveNotificacoes,
  queryFn: () => exigirSucesso(api.GET('/api/notificacoes')),
})

export function useNotificacoes() {
  return useQuery(consultaNotificacoes)
}

function useRecarregar() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: chaveNotificacoes })
}

export function useMarcarLida() {
  const recarregar = useRecarregar()
  return useMutation({
    mutationFn: (id: number) =>
      exigirSucesso(api.POST('/api/notificacoes/{id}/lida', { params: { path: { id } } })),
    onSuccess: recarregar,
  })
}

export function useMarcarTodasLidas() {
  const recarregar = useRecarregar()
  return useMutation({
    mutationFn: () => exigirSucesso(api.POST('/api/notificacoes/lidas')),
    onSuccess: recarregar,
  })
}
