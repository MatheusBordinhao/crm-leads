// Telefone como a API guarda (seção 3.1): E.164 quando o libphonenumber reconhece o número, como `+5551999998888`;
// senão, o texto como veio.

/** Um número do Brasil com DDD em E.164: o DDD (sem zero), os 4 ou 5 primeiros dígitos e os 4 últimos. */
const BRASIL_COM_DDD = /^\+55([1-9]{2})(\d{4,5})(\d{4})$/

/**
 * O telefone como uma pessoa lê na tela: o do Brasil no formato nacional, `(51) 99999-8888`, como no CSV da lista
 * (seção 3.7). O resto volta como está guardado: o número de outro país, o não reconhecido e os especiais, como o
 * 0800, que não têm DDD.
 */
export function formatarTelefone(telefone: string): string {
  const partes = BRASIL_COM_DDD.exec(telefone)
  return partes ? `(${partes[1]}) ${partes[2]}-${partes[3]}` : telefone
}
