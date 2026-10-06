const inteiro = new Intl.NumberFormat('pt-BR')

const porcentagem = new Intl.NumberFormat('pt-BR', { style: 'percent', maximumFractionDigits: 1 })

/** `1.234` */
export function formatarNumero(valor: number): string {
  return inteiro.format(valor)
}

/** Uma taxa de 0 a 1 como porcentagem, com até uma casa: `33,3%`. Sem taxa, um travessão. */
export function formatarTaxa(taxa: number | null): string {
  return taxa === null ? '—' : porcentagem.format(taxa)
}
