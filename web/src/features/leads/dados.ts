import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  api,
  exigirSucesso,
  type CriarLeadRequest,
  type EditarLeadRequest,
  type FiltrosLeads,
} from '@/api/cliente'

const chaveLeads = ['leads'] as const
const chaveLista = [...chaveLeads, 'lista'] as const

/** Cadastros que mudam pouco: ficam em cache por 5 minutos. */
const CINCO_MINUTOS = 5 * 60_000

export function useLeads(filtros: FiltrosLeads) {
  return useQuery({
    queryKey: [...chaveLista, filtros],
    queryFn: () => exigirSucesso(api.GET('/api/leads', { params: { query: filtros } })),
    // Mantém a página anterior na tela enquanto a próxima carrega.
    placeholderData: keepPreviousData,
  })
}

/** Ficha do lead. Um id que não é número (`/leads/abc`) nem chama a API. */
export function useLead(id: number) {
  return useQuery({
    queryKey: [...chaveLeads, 'ficha', id],
    queryFn: () => exigirSucesso(api.GET('/api/leads/{id}', { params: { path: { id } } })),
    enabled: Number.isSafeInteger(id) && id > 0,
  })
}

export function useEtapas() {
  return useQuery({
    queryKey: ['etapas'],
    queryFn: () => exigirSucesso(api.GET('/api/etapas')),
    staleTime: CINCO_MINUTOS,
  })
}

export function useCampanhas() {
  return useQuery({
    queryKey: ['campanhas'],
    queryFn: () => exigirSucesso(api.GET('/api/campanhas')),
    staleTime: CINCO_MINUTOS,
  })
}

export function useCriarLead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (dados: CriarLeadRequest) => exigirSucesso(api.POST('/api/leads', { body: dados })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: chaveLeads }),
  })
}

export function useEditarLead(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (dados: EditarLeadRequest) =>
      exigirSucesso(api.PUT('/api/leads/{id}', { params: { path: { id } }, body: dados })),
    onSuccess: (lead) => {
      queryClient.setQueryData([...chaveLeads, 'ficha', id], lead)
      return queryClient.invalidateQueries({ queryKey: chaveLista })
    },
  })
}
