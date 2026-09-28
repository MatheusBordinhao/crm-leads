import type { ComponentProps } from 'react'
import type { FieldError as ErroDoCampo, UseFormRegisterReturn } from 'react-hook-form'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'

type Props = Omit<ComponentProps<typeof Input>, 'id'> & {
  id: string
  rotulo: string
  registro: UseFormRegisterReturn
  erro?: ErroDoCampo
  descricao?: string
}

/** Campo de texto do React Hook Form, com rótulo, descrição e a mensagem de erro embaixo. */
export function CampoTexto({ id, rotulo, registro, erro, descricao, ...props }: Props) {
  return (
    <Field data-invalid={erro ? true : undefined}>
      <FieldLabel htmlFor={id}>{rotulo}</FieldLabel>
      <Input id={id} aria-invalid={erro ? true : undefined} {...registro} {...props} />
      {descricao && <FieldDescription>{descricao}</FieldDescription>}
      <FieldError errors={[erro]} />
    </Field>
  )
}
