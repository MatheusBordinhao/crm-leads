import { useRouteError } from 'react-router'
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
  return <FalhaAoCarregar />
}

/** No lugar da tela, com o menu em volta. */
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
