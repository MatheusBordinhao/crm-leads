import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { ErroApi } from '@/api/cliente'

/** Chave do usuário logado. `null` quer dizer sem sessão. */
export const chaveEu = ['eu'] as const

/**
 * Um 401 em qualquer chamada quer dizer que a sessão acabou (expirou, usuário desativado ou senha
 * redefinida): o usuário logado vira `null`, e as rotas protegidas levam ao login.
 */
export function criarQueryClient() {
  const aoFalhar = (erro: Error) => {
    if (erro instanceof ErroApi && erro.status === 401) {
      queryClient.setQueryData(chaveEu, null)
    }
  }

  const queryClient: QueryClient = new QueryClient({
    queryCache: new QueryCache({ onError: aoFalhar }),
    mutationCache: new MutationCache({ onError: aoFalhar }),
    defaultOptions: {
      queries: {
        // Erros 4xx não melhoram com uma nova tentativa.
        retry: (falhas, erro) => !(erro instanceof ErroApi && erro.status < 500) && falhas < 2,
      },
    },
  })

  return queryClient
}
