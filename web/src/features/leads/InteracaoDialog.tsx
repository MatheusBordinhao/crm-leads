import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { toast } from 'sonner'
import { z } from 'zod'
import type { TipoInteracao } from '@/api/cliente'
import { CampoSelecao } from '@/components/CampoSelecao'
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
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Textarea } from '@/components/ui/textarea'
import { paraCampoDataHora } from '@/lib/datas'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { useRegistrarInteracao } from './dados'
import { ROTULOS_INTERACAO, TIPOS_INTERACAO } from './rotulos'

const TAMANHO_MAXIMO_DESCRICAO = 2000

const esquema = z
  .object({
    tipo: z.enum(TIPOS_INTERACAO),
    ocorridoEm: z.string().min(1, 'Informe quando aconteceu.'),
    descricao: z
      .string()
      .trim()
      .max(
        TAMANHO_MAXIMO_DESCRICAO,
        `A descrição pode ter até ${TAMANHO_MAXIMO_DESCRICAO} caracteres.`,
      ),
  })
  .superRefine((dados, contexto) => {
    if (dados.tipo === 'nota' && !dados.descricao) {
      contexto.addIssue({ code: 'custom', path: ['descricao'], message: 'Escreva a nota.' })
    }
    // O campo é de minutos: o minuto atual ainda vale.
    if (dados.ocorridoEm && new Date(dados.ocorridoEm).getTime() > Date.now()) {
      contexto.addIssue({
        code: 'custom',
        path: ['ocorridoEm'],
        message: 'A data não pode estar no futuro.',
      })
    }
  })

type Props = {
  leadId: number
  tipoInicial: TipoInteracao
  aoFechar: () => void
}

/**
 * Registro de interação (seção 3.5). A data vem com a hora atual e aceita datas passadas. A interação não é
 * editada depois: uma correção vira uma nova nota.
 */
export function InteracaoDialog({ leadId, tipoInicial, aoFechar }: Props) {
  const registrar = useRegistrarInteracao(leadId)
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [agora] = useState(() => paraCampoDataHora(new Date()))
  const {
    register,
    handleSubmit,
    setError,
    control,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(esquema),
    defaultValues: { tipo: tipoInicial, ocorridoEm: agora, descricao: '' },
  })
  const tipoEscolhido = useWatch({ control, name: 'tipo' })

  const salvar = handleSubmit(({ tipo, ocorridoEm, descricao }) => {
    setErroGeral(null)
    registrar.mutate(
      { tipo, descricao: descricao || null, ocorridoEm: new Date(ocorridoEm).toISOString() },
      {
        onSuccess: () => {
          toast.success('Interação registrada.')
          aoFechar()
        },
        onError: (erro) =>
          setErroGeral(aplicarErrosDaApi(erro, setError, ['tipo', 'descricao', 'ocorridoEm'])),
      },
    )
  })

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent className="max-h-svh overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Registrar interação</DialogTitle>
          <DialogDescription>Depois de salva, a interação não é editada.</DialogDescription>
        </DialogHeader>
        <form onSubmit={salvar} noValidate>
          <FieldGroup>
            {erroGeral && (
              <Alert variant="destructive">
                <AlertDescription>{erroGeral}</AlertDescription>
              </Alert>
            )}
            <CampoSelecao id="tipo" rotulo="Tipo" registro={register('tipo')} erro={errors.tipo}>
              {TIPOS_INTERACAO.map((tipo) => (
                <option key={tipo} value={tipo}>
                  {ROTULOS_INTERACAO[tipo]}
                </option>
              ))}
            </CampoSelecao>
            <CampoTexto
              id="ocorridoEm"
              rotulo="Quando"
              type="datetime-local"
              max={agora}
              registro={register('ocorridoEm')}
              erro={errors.ocorridoEm}
            />
            <Field data-invalid={errors.descricao ? true : undefined}>
              <FieldLabel htmlFor="descricao">Descrição</FieldLabel>
              <Textarea
                id="descricao"
                rows={4}
                aria-invalid={errors.descricao ? true : undefined}
                {...register('descricao')}
              />
              {tipoEscolhido !== 'nota' && <FieldDescription>Opcional.</FieldDescription>}
              <FieldError errors={[errors.descricao]} />
            </Field>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={aoFechar}>
                Cancelar
              </Button>
              <Button type="submit" disabled={registrar.isPending}>
                {registrar.isPending ? 'Salvando…' : 'Registrar'}
              </Button>
            </DialogFooter>
          </FieldGroup>
        </form>
      </DialogContent>
    </Dialog>
  )
}
