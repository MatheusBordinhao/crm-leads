import { useMutation, useQuery } from '@tanstack/react-query'
import {
  api,
  exigirSucesso,
  type AnalisarImportacaoRequest,
  type CriarImportacaoRequest,
} from '@/api/cliente'
import { INTERVALO_ANDAMENTO } from './andamento'

/** Lê o arquivo e devolve a prévia e a sugestão de mapeamento. A API não grava nada. */
export function useAnalisarArquivo() {
  return useMutation({
    mutationFn: (dados: AnalisarImportacaoRequest) =>
      exigirSucesso(api.POST('/api/importacoes/analise', { body: dados })),
  })
}

/** Grava a importação, que é processada em segundo plano. */
export function useCriarImportacao() {
  return useMutation({
    mutationFn: (dados: CriarImportacaoRequest) =>
      exigirSucesso(api.POST('/api/importacoes', { body: dados })),
  })
}

/** Andamento e resultado. Enquanto a importação processa, a tela consulta a cada segundo. */
export function useImportacao(id: number) {
  return useQuery({
    queryKey: ['importacoes', id],
    queryFn: () => exigirSucesso(api.GET('/api/importacoes/{id}', { params: { path: { id } } })),
    enabled: Number.isSafeInteger(id) && id > 0,
    refetchInterval: (consulta) =>
      consulta.state.data?.situacao === 'processando' ? INTERVALO_ANDAMENTO : false,
  })
}

/** O relatório de erros, em CSV. A sessão vai no cookie, como nas chamadas à API. */
export function linkDoRelatorioDeErros(id: number) {
  return `/api/importacoes/${id}/erros`
}
