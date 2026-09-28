import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, ErroApi, exigirSucesso, type EuResponse } from '@/api/cliente'
import { chaveEu } from '@/lib/queryClient'

/** Usuário logado; `null` quando não há sessão. */
export function useEu() {
  return useQuery({
    queryKey: chaveEu,
    queryFn: async (): Promise<EuResponse | null> => {
      const { data, error, response } = await api.GET('/api/auth/eu')
      if (response.status === 401) {
        return null
      }
      if (!response.ok) {
        throw new ErroApi(response.status, error)
      }
      return data ?? null
    },
    staleTime: 60_000,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (dados: { email: string; senha: string }) =>
      exigirSucesso(api.POST('/api/auth/login', { body: dados })),
    onSuccess: (eu) => queryClient.setQueryData(chaveEu, eu),
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => exigirSucesso(api.POST('/api/auth/logout')),
    // Mesmo se a sessão já tiver acabado, o usuário sai: os dados em cache são descartados.
    onSettled: () => {
      queryClient.clear()
      queryClient.setQueryData(chaveEu, null)
    },
  })
}

export function useTrocarSenha() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (dados: { senhaAtual: string; novaSenha: string }) =>
      exigirSucesso(api.POST('/api/auth/trocar-senha', { body: dados })),
    onSuccess: () =>
      queryClient.setQueryData<EuResponse | null>(chaveEu, (eu) =>
        eu ? { ...eu, trocarSenha: false } : eu,
      ),
  })
}
