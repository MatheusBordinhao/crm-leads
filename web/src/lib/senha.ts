/** Mínimo da seção 9 da especificação; a API confere o mesmo valor. */
export const TAMANHO_MINIMO_SENHA = 10

// Sem caracteres que se confundem ao ditar ou digitar no celular (0 e o, 1 e l, i).
const ALFABETO = 'abcdefghjkmnpqrstuvwxyz23456789'

/**
 * Senha provisória aleatória e fácil de digitar, como `k7mp-x2qa-9hdt`: três blocos de quatro
 * caracteres. Usa o gerador criptográfico do navegador, sem viés de módulo.
 */
export function gerarSenha(): string {
  const caracteres: string[] = []
  const limite = 256 - (256 % ALFABETO.length)
  while (caracteres.length < 12) {
    const [byte] = crypto.getRandomValues(new Uint8Array(1))
    if (byte < limite) {
      caracteres.push(ALFABETO[byte % ALFABETO.length])
    }
  }
  return [0, 4, 8].map((inicio) => caracteres.slice(inicio, inicio + 4).join('')).join('-')
}
