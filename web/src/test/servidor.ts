import { setupServer } from 'msw/node'

/**
 * API simulada nos testes. Cada teste registra as respostas de que precisa com `servidor.use(...)`;
 * chamada sem resposta registrada faz o teste falhar.
 */
export const servidor = setupServer()
