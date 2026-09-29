import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  exigirSucesso,
  type CriarUsuarioRequest,
  type EditarUsuarioRequest,
} from '@/api/cliente'
import { chaveEu } from '@/lib/queryClient'

const chaveUsuarios = ['usuarios'] as const

/** Lista de usuários (só admin). Com `enabled: false`, não chama a API: telas da atendente também usam. */
export function useUsuarios({ enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: chaveUsuarios,
    queryFn: () => exigirSucesso(api.GET('/api/usuarios')),
    enabled,
  })
}

/** Depois de qualquer mudança, recarrega a lista e o usuário logado (que pode ter editado a si mesmo). */
function useRecarregar() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: chaveUsuarios }),
      queryClient.invalidateQueries({ queryKey: chaveEu }),
    ])
}

export function useCriarUsuario() {
  const recarregar = useRecarregar()
  return useMutation({
    mutationFn: (dados: CriarUsuarioRequest) =>
      exigirSucesso(api.POST('/api/usuarios', { body: dados })),
    onSuccess: recarregar,
  })
}

export function useEditarUsuario() {
  const recarregar = useRecarregar()
  return useMutation({
    mutationFn: ({ id, dados }: { id: string; dados: EditarUsuarioRequest }) =>
      exigirSucesso(api.PUT('/api/usuarios/{id}', { params: { path: { id } }, body: dados })),
    onSuccess: recarregar,
  })
}

export function useRedefinirSenha() {
  const recarregar = useRecarregar()
  return useMutation({
    mutationFn: ({ id, senhaProvisoria }: { id: string; senhaProvisoria: string }) =>
      exigirSucesso(
        api.POST('/api/usuarios/{id}/redefinir-senha', {
          params: { path: { id } },
          body: { senhaProvisoria },
        }),
      ),
    onSuccess: recarregar,
  })
}
