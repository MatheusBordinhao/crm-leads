import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, exigirSucesso, type EditarConfiguracoesRequest } from '@/api/cliente'
import { chaveFunil } from '@/features/leads/dados'

const chaveEtapas = ['etapas'] as const
const chaveMotivos = ['motivos-perda'] as const
const chaveConfiguracoes = ['configuracoes'] as const

/** Depois de mudar as etapas, recarrega a lista delas e o quadro, que tem uma coluna por etapa. */
function useAposMudarEtapas() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: chaveEtapas }),
      queryClient.invalidateQueries({ queryKey: chaveFunil }),
    ])
}

export function useCriarEtapa() {
  const aposMudar = useAposMudarEtapas()
  return useMutation({
    mutationFn: (nome: string) => exigirSucesso(api.POST('/api/etapas', { body: { nome } })),
    onSuccess: aposMudar,
  })
}

export function useRenomearEtapa() {
  const aposMudar = useAposMudarEtapas()
  return useMutation({
    mutationFn: ({ id, nome }: { id: number; nome: string }) =>
      exigirSucesso(api.PUT('/api/etapas/{id}', { params: { path: { id } }, body: { nome } })),
    onSuccess: aposMudar,
  })
}

export function useReordenarEtapas() {
  const aposMudar = useAposMudarEtapas()
  return useMutation({
    mutationFn: (ids: number[]) => exigirSucesso(api.PUT('/api/etapas/ordem', { body: { ids } })),
    onSuccess: aposMudar,
  })
}

export function useDesativarEtapa() {
  const aposMudar = useAposMudarEtapas()
  return useMutation({
    mutationFn: ({ id, moverLeadsParaId }: { id: number; moverLeadsParaId: number | null }) =>
      exigirSucesso(
        api.POST('/api/etapas/{id}/desativar', {
          params: { path: { id } },
          body: { moverLeadsParaId },
        }),
      ),
    onSuccess: aposMudar,
  })
}

export function useReativarEtapa() {
  const aposMudar = useAposMudarEtapas()
  return useMutation({
    mutationFn: (id: number) =>
      exigirSucesso(api.POST('/api/etapas/{id}/reativar', { params: { path: { id } } })),
    onSuccess: aposMudar,
  })
}

function useAposMudarMotivos() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: chaveMotivos })
}

export function useCriarMotivo() {
  const aposMudar = useAposMudarMotivos()
  return useMutation({
    mutationFn: (nome: string) => exigirSucesso(api.POST('/api/motivos-perda', { body: { nome } })),
    onSuccess: aposMudar,
  })
}

export function useEditarMotivo() {
  const aposMudar = useAposMudarMotivos()
  return useMutation({
    mutationFn: ({ id, nome, ativo }: { id: number; nome: string; ativo: boolean }) =>
      exigirSucesso(
        api.PUT('/api/motivos-perda/{id}', { params: { path: { id } }, body: { nome, ativo } }),
      ),
    onSuccess: aposMudar,
  })
}

export function useConfiguracoes() {
  return useQuery({
    queryKey: chaveConfiguracoes,
    queryFn: () => exigirSucesso(api.GET('/api/configuracoes')),
  })
}

/** A API recebe todas as configurações juntas: quem muda uma manda as outras como estão. */
export function useEditarConfiguracoes() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (configuracoes: EditarConfiguracoesRequest) =>
      exigirSucesso(api.PUT('/api/configuracoes', { body: configuracoes })),
    onSuccess: (configuracoes) => queryClient.setQueryData(chaveConfiguracoes, configuracoes),
  })
}
