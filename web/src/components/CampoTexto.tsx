import { TriangleAlertIcon } from 'lucide-react'
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
  /** Aviso sobre o valor já salvo (por exemplo, telefone não reconhecido), que não impede salvar. */
  aviso?: string
}

/** Campo de texto do React Hook Form, com rótulo, descrição, aviso e a mensagem de erro embaixo. */
export function CampoTexto({ id, rotulo, registro, erro, descricao, aviso, ...props }: Props) {
  return (
    <Field data-invalid={erro ? true : undefined}>
      <FieldLabel htmlFor={id}>{rotulo}</FieldLabel>
      <Input id={id} aria-invalid={erro ? true : undefined} {...registro} {...props} />
      {descricao && <FieldDescription>{descricao}</FieldDescription>}
      {aviso && <AvisoDoCampo>{aviso}</AvisoDoCampo>}
      <FieldError errors={[erro]} />
    </Field>
  )
}

export function AvisoDoCampo({ children }: { children: string }) {
  return (
    <p className="flex items-center gap-1.5 text-sm text-amber-700 dark:text-amber-400">
      <TriangleAlertIcon className="size-4 shrink-0" aria-hidden />
      {children}
    </p>
  )
}
