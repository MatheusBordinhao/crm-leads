import type { ComponentProps } from 'react'
import type { FieldError as ErroDoCampo, UseFormRegisterReturn } from 'react-hook-form'
import { Selecao } from '@/components/Selecao'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'

type Props = Omit<ComponentProps<typeof Selecao>, 'id'> & {
  id: string
  rotulo: string
  registro: UseFormRegisterReturn
  erro?: ErroDoCampo
  descricao?: string
}

/** Seleção do React Hook Form, com rótulo, descrição e a mensagem de erro embaixo. */
export function CampoSelecao({ id, rotulo, registro, erro, descricao, children, ...props }: Props) {
  return (
    <Field data-invalid={erro ? true : undefined}>
      <FieldLabel htmlFor={id}>{rotulo}</FieldLabel>
      <Selecao id={id} aria-invalid={erro ? true : undefined} {...registro} {...props}>
        {children}
      </Selecao>
      {descricao && <FieldDescription>{descricao}</FieldDescription>}
      <FieldError errors={[erro]} />
    </Field>
  )
}
