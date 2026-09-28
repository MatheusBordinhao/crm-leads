import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import type { UsuarioResponse } from '@/api/cliente'
import { CampoTexto } from '@/components/CampoTexto'
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
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from '@/components/ui/field'
import { RadioGroup, RadioGroupItem } from '@/components/ui/radio-group'
import { Switch } from '@/components/ui/switch'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { gerarSenha, TAMANHO_MINIMO_SENHA } from '@/lib/senha'
import { CampoSenhaProvisoria } from './CampoSenhaProvisoria'
import { useCriarUsuario, useEditarUsuario } from './dados'
import { SenhaParaEnviar } from './SenhaParaEnviar'

function criarEsquema(novo: boolean) {
  return z.object({
    nome: z
      .string()
      .trim()
      .min(1, 'Informe o nome.')
      .max(100, 'O nome pode ter até 100 caracteres.'),
    email: z.string().trim().min(1, 'Informe o e-mail.').pipe(z.email('E-mail inválido.')),
    perfil: z.enum(['Atendente', 'Admin']),
    participaRodizio: z.boolean(),
    ativo: z.boolean(),
    senhaProvisoria: novo
      ? z
          .string()
          .min(
            TAMANHO_MINIMO_SENHA,
            `A senha precisa ter pelo menos ${TAMANHO_MINIMO_SENHA} caracteres.`,
          )
      : z.string(),
  })
}

type Dados = z.infer<ReturnType<typeof criarEsquema>>

type Props = {
  /** Sem usuário, cria um novo. */
  usuario?: UsuarioResponse
  /** O admin editando a si mesmo não pode se desativar nem deixar de ser admin. */
  ehOProprio: boolean
  aoFechar: () => void
}

/** Diálogo de novo usuário e de edição. Depois de criar, mostra a senha provisória para enviar. */
export function UsuarioDialog({ usuario, ehOProprio, aoFechar }: Props) {
  const novo = !usuario
  const criar = useCriarUsuario()
  const editar = useEditarUsuario()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [criado, setCriado] = useState<{ nome: string; senha: string } | null>(null)
  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<Dados>({
    resolver: zodResolver(criarEsquema(novo)),
    defaultValues: {
      nome: usuario?.nome ?? '',
      email: usuario?.email ?? '',
      perfil: usuario?.perfil ?? 'Atendente',
      participaRodizio: usuario?.participaRodizio ?? true,
      ativo: usuario?.ativo ?? true,
      senhaProvisoria: novo ? gerarSenha() : '',
    },
  })
  const salvando = criar.isPending || editar.isPending

  const aoFalhar = (erro: Error) =>
    setErroGeral(aplicarErrosDaApi(erro, setError, ['nome', 'email', 'perfil', 'senhaProvisoria']))

  const salvar = handleSubmit(
    ({ nome, email, perfil, participaRodizio, ativo, senhaProvisoria }) => {
      setErroGeral(null)
      if (novo) {
        criar.mutate(
          { nome, email, perfil, participaRodizio, senhaProvisoria },
          {
            onSuccess: (salvo) => setCriado({ nome: salvo.nome, senha: senhaProvisoria }),
            onError: aoFalhar,
          },
        )
      } else {
        editar.mutate(
          { id: usuario.id, dados: { nome, email, perfil, ativo, participaRodizio } },
          {
            onSuccess: () => {
              toast.success('Usuário salvo.')
              aoFechar()
            },
            onError: aoFalhar,
          },
        )
      }
    },
  )

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent className="max-h-svh overflow-y-auto sm:max-w-md">
        <DialogHeader>
          <DialogTitle>
            {criado ? 'Usuário criado' : novo ? 'Novo usuário' : 'Editar usuário'}
          </DialogTitle>
          {!criado && (
            <DialogDescription>
              {novo
                ? 'O usuário entra com uma senha provisória e escolhe uma nova no primeiro acesso.'
                : 'Mudanças de perfil e desativação valem em até 1 minuto.'}
            </DialogDescription>
          )}
        </DialogHeader>

        {criado ? (
          <>
            <SenhaParaEnviar nome={criado.nome} senha={criado.senha} />
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
              <CampoTexto id="nome" rotulo="Nome" registro={register('nome')} erro={errors.nome} />
              <CampoTexto
                id="email"
                rotulo="E-mail"
                type="email"
                autoComplete="off"
                registro={register('email')}
                erro={errors.email}
              />
              <Controller
                control={control}
                name="perfil"
                render={({ field }) => (
                  <FieldSet>
                    <FieldLegend variant="label">Perfil</FieldLegend>
                    <RadioGroup
                      value={field.value}
                      onValueChange={field.onChange}
                      disabled={ehOProprio}
                      className="flex gap-6"
                    >
                      <Field orientation="horizontal">
                        <RadioGroupItem value="Atendente" id="perfil-atendente" />
                        <FieldLabel htmlFor="perfil-atendente">Atendente</FieldLabel>
                      </Field>
                      <Field orientation="horizontal">
                        <RadioGroupItem value="Admin" id="perfil-admin" />
                        <FieldLabel htmlFor="perfil-admin">Admin</FieldLabel>
                      </Field>
                    </RadioGroup>
                  </FieldSet>
                )}
              />
              <Controller
                control={control}
                name="participaRodizio"
                render={({ field }) => (
                  <Field orientation="horizontal">
                    <FieldContent>
                      <FieldLabel htmlFor="participaRodizio">Participa do rodízio</FieldLabel>
                      <FieldDescription>Recebe os leads novos na sua vez.</FieldDescription>
                    </FieldContent>
                    <Switch
                      id="participaRodizio"
                      checked={field.value}
                      onCheckedChange={field.onChange}
                    />
                  </Field>
                )}
              />
              {!novo && (
                <Controller
                  control={control}
                  name="ativo"
                  render={({ field }) => (
                    <Field orientation="horizontal">
                      <FieldContent>
                        <FieldLabel htmlFor="ativo">Ativo</FieldLabel>
                        <FieldDescription>
                          {ehOProprio
                            ? 'Você não pode desativar a si mesmo.'
                            : 'Desativado, não entra no sistema.'}
                        </FieldDescription>
                      </FieldContent>
                      <Switch
                        id="ativo"
                        checked={field.value}
                        onCheckedChange={field.onChange}
                        disabled={ehOProprio}
                      />
                    </Field>
                  )}
                />
              )}
              {novo && (
                <CampoSenhaProvisoria
                  registro={register('senhaProvisoria')}
                  erro={errors.senhaProvisoria}
                  aoGerar={() =>
                    setValue('senhaProvisoria', gerarSenha(), { shouldValidate: true })
                  }
                />
              )}
              <DialogFooter>
                <Button type="button" variant="outline" onClick={aoFechar}>
                  Cancelar
                </Button>
                <Button type="submit" disabled={salvando}>
                  {salvando ? 'Salvando…' : novo ? 'Criar usuário' : 'Salvar'}
                </Button>
              </DialogFooter>
            </FieldGroup>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
