import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { z } from 'zod'
import { CampoTexto } from '@/components/CampoTexto'
import { TelaCentral } from '@/components/layout/TelaCentral'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { FieldGroup } from '@/components/ui/field'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { TAMANHO_MINIMO_SENHA } from '@/lib/senha'
import { useEu, useTrocarSenha } from './sessao'

const esquema = z
  .object({
    senhaAtual: z.string().min(1, 'Informe a senha atual.'),
    novaSenha: z
      .string()
      .min(
        TAMANHO_MINIMO_SENHA,
        `A senha precisa ter pelo menos ${TAMANHO_MINIMO_SENHA} caracteres.`,
      ),
    confirmacao: z.string().min(1, 'Repita a nova senha.'),
  })
  .refine((d) => d.novaSenha !== d.senhaAtual, {
    path: ['novaSenha'],
    message: 'A nova senha precisa ser diferente da atual.',
  })
  .refine((d) => d.confirmacao === d.novaSenha, {
    path: ['confirmacao'],
    message: 'As duas senhas não são iguais.',
  })

type Dados = z.infer<typeof esquema>

export function TrocarSenhaPage() {
  const { data: eu } = useEu()
  const trocar = useTrocarSenha()
  const navigate = useNavigate()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<Dados>({
    resolver: zodResolver(esquema),
    defaultValues: { senhaAtual: '', novaSenha: '', confirmacao: '' },
  })
  const obrigatoria = eu?.trocarSenha === true

  const salvar = handleSubmit(({ senhaAtual, novaSenha }) => {
    setErroGeral(null)
    trocar.mutate(
      { senhaAtual, novaSenha },
      {
        onSuccess: () => {
          toast.success('Senha alterada.')
          navigate('/', { replace: true })
        },
        onError: (erro) =>
          setErroGeral(aplicarErrosDaApi(erro, setError, ['senhaAtual', 'novaSenha'])),
      },
    )
  })

  return (
    <TelaCentral
      titulo="Trocar senha"
      descricao={
        obrigatoria
          ? 'Sua senha é provisória. Escolha uma senha nova para continuar.'
          : `Use pelo menos ${TAMANHO_MINIMO_SENHA} caracteres.`
      }
    >
      <form onSubmit={salvar} noValidate>
        <FieldGroup>
          {erroGeral && (
            <Alert variant="destructive">
              <AlertDescription>{erroGeral}</AlertDescription>
            </Alert>
          )}
          <CampoTexto
            id="senhaAtual"
            rotulo={obrigatoria ? 'Senha provisória' : 'Senha atual'}
            type="password"
            autoComplete="current-password"
            registro={register('senhaAtual')}
            erro={errors.senhaAtual}
          />
          <CampoTexto
            id="novaSenha"
            rotulo="Nova senha"
            type="password"
            autoComplete="new-password"
            registro={register('novaSenha')}
            erro={errors.novaSenha}
          />
          <CampoTexto
            id="confirmacao"
            rotulo="Repita a nova senha"
            type="password"
            autoComplete="new-password"
            registro={register('confirmacao')}
            erro={errors.confirmacao}
          />
          <Button type="submit" disabled={trocar.isPending}>
            {trocar.isPending ? 'Salvando…' : 'Salvar nova senha'}
          </Button>
          {!obrigatoria && (
            <Button variant="ghost" asChild>
              <Link to="/">Cancelar</Link>
            </Button>
          )}
        </FieldGroup>
      </form>
    </TelaCentral>
  )
}
