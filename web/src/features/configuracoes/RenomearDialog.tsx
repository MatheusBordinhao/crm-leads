import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { CampoTexto } from '@/components/CampoTexto'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { FieldGroup } from '@/components/ui/field'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { z } from '@/lib/zod'

type Props = {
  titulo: string
  nomeAtual: string
  tamanhoMaximo: number
  /** Salva o nome novo; o diálogo fecha quando der certo e mostra o erro da API quando não der. */
  aoSalvar: (nome: string) => Promise<unknown>
  aoFechar: () => void
}

/** Renomear uma etapa ou um motivo de perda. */
export function RenomearDialog({ titulo, nomeAtual, tamanhoMaximo, aoSalvar, aoFechar }: Props) {
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(
      z.object({
        nome: z
          .string()
          .trim()
          .min(1, 'Informe o nome.')
          .max(tamanhoMaximo, `O nome pode ter até ${tamanhoMaximo} caracteres.`),
      }),
    ),
    defaultValues: { nome: nomeAtual },
  })

  const salvar = handleSubmit(async ({ nome }) => {
    setErroGeral(null)
    try {
      await aoSalvar(nome)
      aoFechar()
    } catch (erro) {
      setErroGeral(aplicarErrosDaApi(erro, setError, ['nome']))
    }
  })

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{titulo}</DialogTitle>
        </DialogHeader>
        <form onSubmit={salvar} noValidate>
          <FieldGroup>
            {erroGeral && (
              <Alert variant="destructive">
                <AlertDescription>{erroGeral}</AlertDescription>
              </Alert>
            )}
            <CampoTexto
              id="nome-novo"
              rotulo="Nome"
              autoComplete="off"
              registro={register('nome')}
              erro={errors.nome}
            />
            <DialogFooter>
              <Button type="button" variant="outline" onClick={aoFechar}>
                Cancelar
              </Button>
              <Button type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Salvando…' : 'Salvar'}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}
