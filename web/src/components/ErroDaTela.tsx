import { useEffect } from 'react'
import { useRouteError } from 'react-router'
import { Carregando } from '@/components/Carregando'
import { Button } from '@/components/ui/button'
import { ErroAoCarregarTela, recarregarPagina } from '@/lib/recarga'

/**
 * O ErrorBoundary das telas sob demanda (`tela()`, em rotas.tsx). Trata só a falha ao carregar o arquivo da tela, como
 * depois de uma publicação nova com o CRM aberto. Os outros erros sobem para o ErrorBoundary padrão do React Router,
 * como antes das telas sob demanda.
 */
export function ErroDaTela() {
  const erro = useRouteError()
  if (!(erro instanceof ErroAoCarregarTela)) {
    throw erro
  }
  return erro.recarregarSozinha ? <Recarregando /> : <FalhaAoCarregar />
}

/**
 * A página recarrega já no endereço da tela, porque o roteador fica nele com o erro, e abre na versão nova. Até lá, o
 * Carregando fica no lugar da tela, sem piscar o erro.
 */
function Recarregando() {
  useEffect(() => recarregarPagina(), [])
  return <Carregando />
}

/** Com uma recarga automática há menos de um minuto: o erro no lugar da tela, com o menu em volta. */
function FalhaAoCarregar() {
  return (
    <div role="alert" className="flex flex-col items-center gap-3 p-8 text-center">
      <p>Não foi possível abrir esta tela. Confira a conexão e recarregue a página.</p>
      <Button variant="outline" onClick={() => recarregarPagina()}>
        Recarregar a página
      </Button>
    </div>
  )
}
