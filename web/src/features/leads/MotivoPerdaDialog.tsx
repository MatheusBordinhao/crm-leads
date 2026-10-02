import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { CampoSelecao } from '@/components/CampoSelecao'
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
import { Field, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { z } from '@/lib/zod'
import { useMotivosPerda } from './dados'

const TAMANHO_MAXIMO_DETALHE = 500

const esquema = z.object({
  motivoPerdaId: z.string().min(1, 'Escolha o motivo da perda.'),
  detalhe: z
    .string()
    .trim()
    .max(TAMANHO_MAXIMO_DETALHE, `O detalhe pode ter até ${TAMANHO_MAXIMO_DETALHE} caracteres.`),
})

export type MotivoEscolhido = { motivoPerdaId: number; motivoPerdaDetalhe: string | null }

type Props = {
  nomeDoLead: string
  salvando?: boolean
  /** Falha ao salvar, mostrada no diálogo. */
  erro?: string | null
  aoConfirmar: (motivo: MotivoEscolhido) => void
  aoCancelar: () => void
}

/**
 * Pedido do motivo ao levar um lead para Perdido (seção 3.4), na ficha e no quadro. Cancelar não muda nada:
 * o lead continua na etapa em que estava.
 */
export function MotivoPerdaDialog({ nomeDoLead, salvando, erro, aoConfirmar, aoCancelar }: Props) {
  const { data: motivos } = useMotivosPerda()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm({ resolver: zodResolver(esquema), defaultValues: { motivoPerdaId: '', detalhe: '' } })

  const confirmar = handleSubmit(({ motivoPerdaId, detalhe }) =>
    aoConfirmar({ motivoPerdaId: Number(motivoPerdaId), motivoPerdaDetalhe: detalhe || null }),
  )

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoCancelar()}>
      <DialogContent className="max-h-svh overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Motivo da perda</DialogTitle>
          <DialogDescription>Por que o lead de {nomeDoLead} foi perdido?</DialogDescription>
        </DialogHeader>
        <form onSubmit={confirmar} noValidate>
          <FieldGroup>
            {erro && (
              <Alert variant="destructive">
                <AlertDescription>{erro}</AlertDescription>
              </Alert>
            )}
            <CampoSelecao
              id="motivoPerdaId"
              rotulo="Motivo"
              registro={register('motivoPerdaId')}
              erro={errors.motivoPerdaId}
            >
              <option value="">Escolha o motivo</option>
              {motivos
                ?.filter((motivo) => motivo.ativo)
                .map((motivo) => (
                  <option key={motivo.id} value={motivo.id}>
                    {motivo.nome}
                  </option>
                ))}
            </CampoSelecao>
            <Field data-invalid={errors.detalhe ? true : undefined}>
              <FieldLabel htmlFor="motivoPerdaDetalhe">Detalhe (opcional)</FieldLabel>
              <Textarea
                id="motivoPerdaDetalhe"
                aria-invalid={errors.detalhe ? true : undefined}
                {...register('detalhe')}
              />
              <FieldError errors={[errors.detalhe]} />
            </Field>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={aoCancelar}>
                Cancelar
              </Button>
              <Button type="submit" disabled={salvando}>
                {salvando ? 'Salvando…' : 'Marcar como perdido'}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}
