import { QueryClientProvider, type QueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Toaster } from '@/components/ui/sonner'
import { criarConexao as criarConexaoReal, type CriarConexao } from '@/features/tempo-real/conexao'
import { ContextoTempoReal } from '@/features/tempo-real/contexto'
import { useConsultaDeMidia } from '@/lib/consultaDeMidia'

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
  // Abaixo do md (até 767 px), os avisos saem embaixo da tela: em cima, cobririam o cabeçalho, a gaveta do menu e o
  // sino. No computador, continuam em cima, no centro.
  const computador = useConsultaDeMidia('(min-width: 768px)')
  return (
    <QueryClientProvider client={queryClient}>
      <ContextoTempoReal value={criarConexao}>
        {children}
        <Toaster position={computador ? 'top-center' : 'bottom-center'} richColors />
      </ContextoTempoReal>
    </QueryClientProvider>
  )
}
