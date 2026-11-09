// A tela que não carrega (Lote 4B). As telas são carregadas sob demanda (`tela()`, em rotas.tsx): depois de uma
// publicação nova, quem está com o CRM aberto pode pedir o arquivo de uma tela que não existe mais no servidor (404),
// ou a conexão pode cair no meio. O ErroDaTela mostra o erro, com o botão de recarregar.

/** A falha ao carregar o arquivo de uma tela (o import do `lazy` das rotas). */
export class ErroAoCarregarTela extends Error {
  constructor(causa: unknown) {
    super('Não foi possível carregar a tela.', { cause: causa })
    this.name = 'ErroAoCarregarTela'
  }
}

/** Recarrega a página. Fica num módulo próprio para os testes a trocarem por `vi.mock`: o jsdom não recarrega. */
export function recarregarPagina() {
  window.location.reload()
}
