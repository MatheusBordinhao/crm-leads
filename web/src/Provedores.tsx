import { QueryClientProvider, type QueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Toaster } from '@/components/ui/sonner'
import { criarConexao as criarConexaoReal, type CriarConexao } from '@/features/tempo-real/conexao'
import { ContextoTempoReal } from '@/features/tempo-real/contexto'

/** Provedores da aplicação, usados pelo main.tsx e pelos testes, que trocam a conexão com o hub por uma falsa. */
export function Provedores({
  queryClient,
  criarConexao = criarConexaoReal,
  children,
}: {
  queryClient: QueryClient
  criarConexao?: CriarConexao
  children: ReactNode
}) {
  return (
    <QueryClientProvider client={queryClient}>
      <ContextoTempoReal value={criarConexao}>
        {children}
        <Toaster position="top-center" richColors />
      </ContextoTempoReal>
    </QueryClientProvider>
  )
}
