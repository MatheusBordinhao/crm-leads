import { Navigate, Outlet, useLocation, type NavigateProps } from 'react-router'
import { Carregando } from '@/components/Carregando'
import { Button } from '@/components/ui/button'
import { useEu } from './sessao'

/**
 * Redireciona e mostra o Carregando enquanto a tela de destino carrega: as telas são sob demanda (rotas.tsx), e o
 * roteador só troca de tela depois de baixar o arquivo dela. Com o Navigate sozinho, a área ficaria vazia até lá.
 */
function Redirecionar(props: NavigateProps) {
  return (
    <>
      <Navigate {...props} />
      <Carregando />
    </>
  )
}

/** Exige sessão. Com a senha provisória, qualquer rota leva à troca de senha. */
export function RotaProtegida() {
  const { data: eu, isPending, isError, refetch } = useEu()
  const { pathname } = useLocation()

  if (isPending) {
    return <Carregando />
  }
  if (isError) {
    return (
      <div className="flex flex-col items-center gap-3 p-8 text-center">
        <p>Não foi possível falar com o servidor.</p>
        <Button variant="outline" onClick={() => refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }
  if (!eu) {
    return <Redirecionar to="/login" state={{ voltarPara: pathname }} replace />
  }
  if (eu.trocarSenha && pathname !== '/trocar-senha') {
    return <Redirecionar to="/trocar-senha" replace />
  }
  return <Outlet />
}

/** A tela inicial de cada perfil: o painel para o admin e o funil, onde ela trabalha, para a atendente. */
export function TelaInicial() {
  const { data: eu } = useEu()
  return <Redirecionar to={eu?.perfil === 'Admin' ? '/painel' : '/funil'} replace />
}

/** Só para admin. A atendente volta ao início (a API também responde 403). */
export function RotaAdmin() {
  const { data: eu } = useEu()
  return eu?.perfil === 'Admin' ? <Outlet /> : <Redirecionar to="/" replace />
}
