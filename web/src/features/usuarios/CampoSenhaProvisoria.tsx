import type { FieldError as ErroDoCampo, UseFormRegisterReturn } from 'react-hook-form'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { TAMANHO_MINIMO_SENHA } from '@/lib/senha'

/** Campo da senha provisória, com o botão que gera uma senha aleatória. */
export function CampoSenhaProvisoria({
  registro,
  erro,
  aoGerar,
}: {
  registro: UseFormRegisterReturn
  erro?: ErroDoCampo
  aoGerar: () => void
}) {
  return (
    <Field data-invalid={erro ? true : undefined}>
      <FieldLabel htmlFor="senhaProvisoria">Senha provisória</FieldLabel>
      <div className="flex gap-2">
        <Input
          id="senhaProvisoria"
          autoComplete="off"
          spellCheck={false}
          aria-invalid={erro ? true : undefined}
          {...registro}
        />
        <Button type="button" variant="outline" onClick={aoGerar}>
          Gerar
        </Button>
      </div>
      <FieldDescription>
        Pelo menos {TAMANHO_MINIMO_SENHA} caracteres. A pessoa troca no primeiro acesso.
      </FieldDescription>
      <FieldError errors={[erro]} />
    </Field>
  )
}
