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

/** Lista, fichas e linhas do tempo: a importação recarrega tudo quando termina. */
export const chaveLeads = ['leads'] as const
export const chaveLista = [...chaveLeads, 'lista'] as const
export const chaveFicha = (id: number) => [...chaveLeads, 'ficha', id] as const
export const chaveLinhaDoTempo = (id: number) => [...chaveLeads, 'linha-do-tempo', id] as const

/** Quadro do funil. Fica aqui porque toda mudança de etapa, na ficha ou no quadro, o recarrega. */
export const chaveFunil = ['funil'] as const

/** Etapas e motivos de perda, que o tempo real recarrega quando o admin os muda. */
export const chaveEtapas = ['etapas'] as const
export const chaveMotivosPerda = ['motivos-perda'] as const

/**
 * As mudanças de etapa, na ficha e no quadro. Enquanto uma está a caminho da API, o tempo real não recarrega o
 * quadro (o cartão voltaria para a coluna antiga até a resposta); ela mesma o recarrega quando termina.
 */
export const chaveMudarEtapa = ['mudar-etapa'] as const

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
    queryKey: chaveEtapas,
    queryFn: () => exigirSucesso(api.GET('/api/etapas')),
    staleTime: CINCO_MINUTOS,
  })
}

export function useMotivosPerda() {
  return useQuery({
    queryKey: chaveMotivosPerda,
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
    mutationKey: chaveMudarEtapa,
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

/**
 * Anonimiza o lead, sem volta (só admin). A ficha fica com a versão anonimizada, e a lista, o quadro e a linha do tempo,
 * que perdeu as descrições, são recarregados.
 */
export function useAnonimizarLead(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () =>
      exigirSucesso(api.POST('/api/leads/{id}/anonimizar', { params: { path: { id } } })),
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

/** Exclui de vez um lead de teste (só admin). A ficha sai do cache, e a lista e o quadro são recarregados. */
export function useExcluirLead(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => exigirSucesso(api.DELETE('/api/leads/{id}', { params: { path: { id } } })),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: chaveFicha(id) })
      queryClient.removeQueries({ queryKey: chaveLinhaDoTempo(id) })
      return Promise.all([
        queryClient.invalidateQueries({ queryKey: chaveLista }),
        queryClient.invalidateQueries({ queryKey: chaveFunil }),
      ])
    },
  })
}

/** O arquivo com os dados do titular. A sessão vai no cookie, como no CSV do relatório. */
export function linkDosDadosDoTitular(id: number) {
  return `/api/leads/${id}/dados-titular`
}

/** A lista em CSV, com os filtros e a ordem da tela, sem a página: a exportação traz todos os leads. */
export function linkDaExportacao(filtros: FiltrosLeads) {
  const params = new URLSearchParams()
  for (const [nome, valor] of Object.entries(filtros)) {
    if (
      valor !== undefined &&
      valor !== null &&
      valor !== '' &&
      nome !== 'pagina' &&
      nome !== 'tamanho'
    ) {
      params.set(nome, String(valor))
    }
  }
  const query = params.toString()
  return `/api/leads/exportacao${query ? `?${query}` : ''}`
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
