// Datas vêm da API em UTC e são exibidas no horário de Brasília.
const FUSO = 'America/Sao_Paulo'

const dataHora = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const hora = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO,
  hour: '2-digit',
  minute: '2-digit',
})

/** `05/10/2026, 14:32` */
export function formatarDataHora(iso: string): string {
  return dataHora.format(new Date(iso))
}

/** `14:32` */
export function formatarHora(iso: string): string {
  return hora.format(new Date(iso))
}
