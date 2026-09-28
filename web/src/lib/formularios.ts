import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ErroApi } from '@/api/cliente'

/**
 * Mostra nos campos do formulário os erros de validação que a API devolveu (400). Devolve a mensagem
 * geral para exibir fora dos campos, ou `null` se todos os erros couberam em algum campo.
 */
export function aplicarErrosDaApi<T extends FieldValues>(
  erro: unknown,
  setError: UseFormSetError<T>,
  campos: readonly Path<T>[],
): string | null {
  if (!(erro instanceof ErroApi)) {
    return 'Não foi possível falar com o servidor. Confira a conexão e tente de novo.'
  }

  const foraDosCampos: string[] = []
  for (const [campo, mensagens] of Object.entries(erro.errosDosCampos)) {
    if ((campos as readonly string[]).includes(campo)) {
      setError(campo as Path<T>, { message: mensagens[0] })
    } else {
      foraDosCampos.push(...mensagens)
    }
  }

  if (foraDosCampos.length > 0) {
    return foraDosCampos.join(' ')
  }
  return Object.keys(erro.errosDosCampos).length > 0 ? null : erro.message
}
