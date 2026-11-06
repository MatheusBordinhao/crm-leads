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
  // Os avisos saem embaixo da tela, no centro, em todas as larguras. Em cima, cobriam o cabeçalho, a gaveta do menu e
  // o sino no celular e, no computador, a faixa do cabeçalho e o título da página.
  return (
    <QueryClientProvider client={queryClient}>
      <ContextoTempoReal value={criarConexao}>
        {children}
        <Toaster position="bottom-center" richColors />
      </ContextoTempoReal>
    </QueryClientProvider>
  )
}
