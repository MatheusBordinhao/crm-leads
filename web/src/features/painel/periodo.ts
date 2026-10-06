/** Atalhos de período do painel. Os últimos 30 dias são o padrão da API e não vão para o endereço. */
export type Periodo = '7d' | '30d' | '90d' | 'mes' | 'mes-passado' | 'personalizado'

export const ROTULOS_PERIODO: Record<Periodo, string> = {
  '7d': 'Últimos 7 dias',
  '30d': 'Últimos 30 dias',
  '90d': 'Últimos 90 dias',
  mes: 'Este mês',
  'mes-passado': 'Mês passado',
  personalizado: 'Personalizado',
}

export const PERIODOS = Object.keys(ROTULOS_PERIODO) as Periodo[]

/** Datas que a API aceita no período. */
export const MENOR_DIA = '2000-01-01'
export const MAIOR_DIA = '2099-12-31'

const FORMATO_DIA = /^\d{4}-\d{2}-\d{2}$/

const diaEmBrasilia = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
})

/** O dia de hoje no horário de Brasília, no formato da API e dos campos de data: `2026-10-06`. */
export function hojeEmBrasilia(agora: Date = new Date()): string {
  const partes = Object.fromEntries(
    diaEmBrasilia.formatToParts(agora).map((p) => [p.type, p.value]),
  )
  return `${partes.year}-${partes.month}-${partes.day}`
}

/** O dia `dias` depois (ou antes) de `dia`, contado no calendário, sem fuso. */
function somarDias(dia: string, dias: number): string {
  const [ano, mes, diaDoMes] = dia.split('-').map(Number)
  return new Date(Date.UTC(ano, mes - 1, diaDoMes + dias)).toISOString().slice(0, 10)
}

/** O primeiro dia do mês de `dia`, ou de `meses` meses antes ou depois. */
function inicioDoMes(dia: string, meses = 0): string {
  const [ano, mes] = dia.split('-').map(Number)
  return new Date(Date.UTC(ano, mes - 1 + meses, 1)).toISOString().slice(0, 10)
}

/** O intervalo de um atalho, em dias de Brasília, com o primeiro e o último incluídos. */
export function intervaloDoAtalho(
  periodo: Exclude<Periodo, 'personalizado'>,
  hoje: string,
): { de: string; ate: string } {
  switch (periodo) {
    case '7d':
      return { de: somarDias(hoje, -6), ate: hoje }
    case '30d':
      return { de: somarDias(hoje, -29), ate: hoje }
    case '90d':
      return { de: somarDias(hoje, -89), ate: hoje }
    case 'mes':
      return { de: inicioDoMes(hoje), ate: hoje }
    case 'mes-passado':
      return { de: inicioDoMes(hoje, -1), ate: somarDias(inicioDoMes(hoje), -1) }
  }
}

function dia(params: URLSearchParams, nome: string) {
  const valor = params.get(nome)
  return valor && FORMATO_DIA.test(valor) ? valor : undefined
}

/**
 * O período do endereço: com `de` ou `ate`, é o personalizado; senão, o atalho de `periodo`; sem nada, os 30 dias.
 */
export function lerPeriodo(params: URLSearchParams): Periodo {
  if (dia(params, 'de') || dia(params, 'ate')) {
    return 'personalizado'
  }
  const periodo = params.get('periodo') as Periodo | null
  return periodo && periodo !== 'personalizado' && PERIODOS.includes(periodo) ? periodo : '30d'
}

/** As datas que vão para a API. Nos 30 dias, nenhuma: a API usa o padrão dela, com o hoje dela. */
export function intervaloDoEndereco(
  params: URLSearchParams,
  hoje: string,
): { de?: string; ate?: string } {
  const periodo = lerPeriodo(params)
  if (periodo === 'personalizado') {
    return { de: dia(params, 'de'), ate: dia(params, 'ate') }
  }
  return periodo === '30d' ? {} : intervaloDoAtalho(periodo, hoje)
}
