// CPF e CNPJ como na API (seção 3.1): só letras e números, em maiúsculas. 11 caracteres é CPF; 14 é CNPJ,
// numérico ou alfanumérico (desde julho de 2026).

const PESOS_PRIMEIRO_DIGITO_CNPJ = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
const PESOS_SEGUNDO_DIGITO_CNPJ = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]

/** Tira máscara e espaços e passa para maiúsculas: `12.abc.345/01de-35` vira `12ABC34501DE35`. */
export function normalizarDocumento(valor: string): string {
  return valor.replace(/[^0-9a-z]/gi, '').toUpperCase()
}

/** Confere os dígitos verificadores de um valor já normalizado. */
export function documentoValido(normalizado: string): boolean {
  if (normalizado.length === 11) {
    return cpfValido(normalizado)
  }
  return normalizado.length === 14 && cnpjValido(normalizado)
}

/** Resto da divisão por 11 menor que 2 dá 0; senão, 11 menos o resto. */
function digito(soma: number) {
  const resto = soma % 11
  return resto < 2 ? 0 : 11 - resto
}

function cpfValido(cpf: string) {
  // Sequências como 111.111.111-11 passam na conta, mas não são CPFs.
  if (!/^\d{11}$/.test(cpf) || /^(\d)\1{10}$/.test(cpf)) {
    return false
  }
  const digitos = [...cpf].map(Number)
  const verificador = (quantidade: number) =>
    digito(digitos.slice(0, quantidade).reduce((soma, d, i) => soma + d * (quantidade + 1 - i), 0))
  return verificador(9) === digitos[9] && verificador(10) === digitos[10]
}

function cnpjValido(cnpj: string) {
  if (!/^[0-9A-Z]{12}\d{2}$/.test(cnpj) || /^(.)\1{13}$/.test(cnpj)) {
    return false
  }
  // Cada caractere vale o código ASCII menos 48: os números valem eles mesmos, A vale 17 e Z vale 42.
  const valores = [...cnpj].map((c) => c.charCodeAt(0) - 48)
  const verificador = (pesos: number[]) =>
    digito(pesos.reduce((soma, peso, i) => soma + valores[i] * peso, 0))
  return (
    verificador(PESOS_PRIMEIRO_DIGITO_CNPJ) === valores[12] &&
    verificador(PESOS_SEGUNDO_DIGITO_CNPJ) === valores[13]
  )
}
