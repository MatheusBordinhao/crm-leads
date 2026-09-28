import { Navigate, Outlet, useLocation } from 'react-router'
import { Carregando } from '@/components/Carregando'
import { Button } from '@/components/ui/button'
import { useEu } from './sessao'

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
    return <Navigate to="/login" state={{ voltarPara: pathname }} replace />
  }
  if (eu.trocarSenha && pathname !== '/trocar-senha') {
    return <Navigate to="/trocar-senha" replace />
  }
  return <Outlet />
}

/** Só para admin. A atendente volta ao início (a API também responde 403). */
export function RotaAdmin() {
  const { data: eu } = useEu()
  return eu?.perfil === 'Admin' ? <Outlet /> : <Navigate to="/" replace />
}
