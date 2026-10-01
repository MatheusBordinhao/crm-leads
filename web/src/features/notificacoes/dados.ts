import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, exigirSucesso } from '@/api/cliente'

const chaveNotificacoes = ['notificacoes'] as const

/** De quanto em quanto tempo a lista é consultada, até o tempo real chegar (Passo 7). */
export const INTERVALO_NOTIFICACOES = 30_000

/** As notificações mais recentes do usuário logado e quantas não foram lidas, consultadas a cada 30 segundos. */
export function useNotificacoes() {
  return useQuery({
    queryKey: chaveNotificacoes,
    queryFn: () => exigirSucesso(api.GET('/api/notificacoes')),
    refetchInterval: INTERVALO_NOTIFICACOES,
  })
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
