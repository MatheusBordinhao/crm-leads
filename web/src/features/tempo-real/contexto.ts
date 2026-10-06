import { createContext, useContext } from 'react'
import { criarConexao, type CriarConexao } from './conexao'

/** A fábrica da conexão com o hub. Os testes trocam pela do hub falso (test/hubFalso.ts). */
export const ContextoTempoReal = createContext<CriarConexao>(criarConexao)

export function useCriarConexao() {
  return useContext(ContextoTempoReal)
}
