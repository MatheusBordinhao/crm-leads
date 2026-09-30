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

const relativo = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' })

/** Quanto tempo passou: `agora`, `há 5 min`, `há 3 h`, `ontem`, `há 12 dias`. */
export function tempoDesde(iso: string, agora: Date = new Date()): string {
  const minutos = Math.floor((agora.getTime() - new Date(iso).getTime()) / 60_000)
  if (minutos < 1) {
    return 'agora'
  }
  if (minutos < 60) {
    return `há ${minutos} min`
  }
  const horas = Math.floor(minutos / 60)
  if (horas < 24) {
    return `há ${horas} h`
  }
  return relativo.format(-Math.floor(horas / 24), 'day')
}

/**
 * Valor para um `<input type="datetime-local">`: `2026-10-05T14:03`, no fuso do navegador, que é o que o
 * campo mostra e devolve.
 */
export function paraCampoDataHora(data: Date): string {
  const doisDigitos = (valor: number) => String(valor).padStart(2, '0')
  return (
    `${data.getFullYear()}-${doisDigitos(data.getMonth() + 1)}-${doisDigitos(data.getDate())}` +
    `T${doisDigitos(data.getHours())}:${doisDigitos(data.getMinutes())}`
  )
}
