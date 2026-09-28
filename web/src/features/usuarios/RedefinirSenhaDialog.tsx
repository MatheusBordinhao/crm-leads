import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import type { UsuarioResponse } from '@/api/cliente'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { FieldGroup } from '@/components/ui/field'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { gerarSenha, TAMANHO_MINIMO_SENHA } from '@/lib/senha'
import { CampoSenhaProvisoria } from './CampoSenhaProvisoria'
import { useRedefinirSenha } from './dados'
import { SenhaParaEnviar } from './SenhaParaEnviar'

const esquema = z.object({
  senhaProvisoria: z
    .string()
    .min(
      TAMANHO_MINIMO_SENHA,
      `A senha precisa ter pelo menos ${TAMANHO_MINIMO_SENHA} caracteres.`,
    ),
})

type Dados = z.infer<typeof esquema>

/** Troca a senha do usuário por uma provisória. Também tira o bloqueio por tentativas erradas. */
export function RedefinirSenhaDialog({
  usuario,
  aoFechar,
}: {
  usuario: UsuarioResponse
  aoFechar: () => void
}) {
  const redefinir = useRedefinirSenha()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [senhaDefinida, setSenhaDefinida] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<Dados>({
    resolver: zodResolver(esquema),
    defaultValues: { senhaProvisoria: gerarSenha() },
  })

  const salvar = handleSubmit(({ senhaProvisoria }) => {
    setErroGeral(null)
    redefinir.mutate(
      { id: usuario.id, senhaProvisoria },
      {
        onSuccess: () => setSenhaDefinida(senhaProvisoria),
        onError: (erro) => setErroGeral(aplicarErrosDaApi(erro, setError, ['senhaProvisoria'])),
      },
    )
  })

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Redefinir a senha de {usuario.nome}</DialogTitle>
          {!senhaDefinida && (
            <DialogDescription>
              A senha atual deixa de valer, a sessão aberta é encerrada e o bloqueio por tentativas
              erradas sai.
            </DialogDescription>
          )}
        </DialogHeader>

        {senhaDefinida ? (
          <>
            <SenhaParaEnviar nome={usuario.nome} senha={senhaDefinida} />
            <DialogFooter>
              <Button onClick={aoFechar}>Concluir</Button>
            </DialogFooter>
          </>
        ) : (
          <form onSubmit={salvar} noValidate>
            <FieldGroup>
              {erroGeral && (
                <Alert variant="destructive">
                  <AlertDescription>{erroGeral}</AlertDescription>
                </Alert>
              )}
              <CampoSenhaProvisoria
                registro={register('senhaProvisoria')}
                erro={errors.senhaProvisoria}
                aoGerar={() => setValue('senhaProvisoria', gerarSenha(), { shouldValidate: true })}
              />
              <DialogFooter>
                <Button type="button" variant="outline" onClick={aoFechar}>
                  Cancelar
                </Button>
                <Button type="submit" disabled={redefinir.isPending}>
                  {redefinir.isPending ? 'Salvando…' : 'Redefinir senha'}
                </Button>
              </DialogFooter>
            </FieldGroup>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
