import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { z } from 'zod'
import { CampoTexto } from '@/components/CampoTexto'
import { TelaCentral } from '@/components/layout/TelaCentral'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { FieldGroup } from '@/components/ui/field'
import { useEu, useLogin } from './sessao'

const esquema = z.object({
  email: z.string().trim().min(1, 'Informe o e-mail.'),
  senha: z.string().min(1, 'Informe a senha.'),
})

type Dados = z.infer<typeof esquema>

export function LoginPage() {
  const { data: eu } = useEu()
  const login = useLogin()
  const navigate = useNavigate()
  const { state } = useLocation() as { state: { voltarPara?: string } | null }
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Dados>({ resolver: zodResolver(esquema), defaultValues: { email: '', senha: '' } })

  if (eu && !login.isSuccess) {
    return <Navigate to="/" replace />
  }

  const entrar = handleSubmit((dados) =>
    login.mutate(dados, {
      onSuccess: (logado) =>
        navigate(logado.trocarSenha ? '/trocar-senha' : (state?.voltarPara ?? '/'), {
          replace: true,
        }),
    }),
  )

  return (
    <TelaCentral titulo="Entrar">
      <form onSubmit={entrar} noValidate>
        <FieldGroup>
          {login.isError && (
            <Alert variant="destructive">
              <AlertDescription>{login.error.message}</AlertDescription>
            </Alert>
          )}
          <CampoTexto
            id="email"
            rotulo="E-mail"
            type="email"
            autoComplete="username"
            registro={register('email')}
            erro={errors.email}
          />
          <CampoTexto
            id="senha"
            rotulo="Senha"
            type="password"
            autoComplete="current-password"
            registro={register('senha')}
            erro={errors.senha}
          />
          <Button type="submit" disabled={login.isPending}>
            {login.isPending ? 'Entrando…' : 'Entrar'}
          </Button>
        </FieldGroup>
      </form>
    </TelaCentral>
  )
}
