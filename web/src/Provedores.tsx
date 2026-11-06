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
        {/* No celular (até 600 px, no sonner), os avisos ficam abaixo do cabeçalho de 56 px, sem cobrir o menu e o sino. */}
        <Toaster position="top-center" richColors mobileOffset={{ top: 64 }} />
      </ContextoTempoReal>
    </QueryClientProvider>
  )
}
