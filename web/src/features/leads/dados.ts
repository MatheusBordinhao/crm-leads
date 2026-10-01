import {
  keepPreviousData,
  useMutation,
  useQuery,
  useQueryClient,
  type QueryClient,
} from '@tanstack/react-query'
import {
  api,
  exigirSucesso,
  type CriarLeadRequest,
  type EditarLeadRequest,
  type FiltrosLeads,
  type LeadResponse,
  type MudarEtapaRequest,
  type RegistrarInteracaoRequest,
} from '@/api/cliente'

const chaveLeads = ['leads'] as const
const chaveLista = [...chaveLeads, 'lista'] as const
const chaveFicha = (id: number) => [...chaveLeads, 'ficha', id] as const
const chaveLinhaDoTempo = (id: number) => [...chaveLeads, 'linha-do-tempo', id] as const

/** Quadro do funil. Fica aqui porque toda mudança de etapa, na ficha ou no quadro, o recarrega. */
export const chaveFunil = ['funil'] as const

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
    queryKey: chaveFicha(id),
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

export function useMotivosPerda() {
  return useQuery({
    queryKey: ['motivos-perda'],
    queryFn: () => exigirSucesso(api.GET('/api/motivos-perda')),
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
      queryClient.setQueryData(chaveFicha(id), lead)
      return queryClient.invalidateQueries({ queryKey: chaveLista })
    },
  })
}

export type MudancaDeEtapa = { leadId: number; dados: MudarEtapaRequest }

export function mudarEtapa({ leadId, dados }: MudancaDeEtapa) {
  return exigirSucesso(
    api.POST('/api/leads/{id}/etapa', { params: { path: { id: leadId } }, body: dados }),
  )
}

/** Depois de mudar a etapa: a ficha fica com a versão nova, e a lista, o quadro e o histórico são recarregados. */
export function aposMudarEtapa(queryClient: QueryClient, lead: LeadResponse) {
  queryClient.setQueryData(chaveFicha(lead.id), lead)
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: chaveLista }),
    queryClient.invalidateQueries({ queryKey: chaveLinhaDoTempo(lead.id) }),
    queryClient.invalidateQueries({ queryKey: chaveFunil }),
  ])
}

export function useMudarEtapa() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: mudarEtapa,
    onSuccess: (lead) => aposMudarEtapa(queryClient, lead),
  })
}

/** Passa o lead para outra pessoa (só admin). A troca aparece na linha do tempo, na lista e no quadro. */
export function useTrocarResponsavel(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (responsavelId: string) =>
      exigirSucesso(
        api.POST('/api/leads/{id}/responsavel', {
          params: { path: { id } },
          body: { responsavelId },
        }),
      ),
    onSuccess: (lead) => {
      queryClient.setQueryData(chaveFicha(id), lead)
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: chaveLista }),
        queryClient.invalidateQueries({ queryKey: chaveLinhaDoTempo(id) }),
        queryClient.invalidateQueries({ queryKey: chaveFunil }),
      ])
    },
  })
}

export function useLinhaDoTempo(id: number) {
  return useQuery({
    queryKey: chaveLinhaDoTempo(id),
    queryFn: () =>
      exigirSucesso(api.GET('/api/leads/{id}/linha-do-tempo', { params: { path: { id } } })),
  })
}

/** Registra a interação; a ficha é recarregada porque a primeira ligação grava o primeiro contato. */
export function useRegistrarInteracao(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (dados: RegistrarInteracaoRequest) =>
      exigirSucesso(
        api.POST('/api/leads/{id}/interacoes', { params: { path: { id } }, body: dados }),
      ),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: chaveLinhaDoTempo(id) }),
        queryClient.invalidateQueries({ queryKey: chaveFicha(id) }),
      ]),
  })
}
