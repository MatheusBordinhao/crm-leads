import { http, HttpResponse } from 'msw'
import type { RelatorioCampanhasResponse } from '@/api/cliente'
import { servidor } from './servidor'

/** Relatório de setembro com os números da massa conhecida da API (MassaDoPainel). */
export function relatorio(
  parcial: Partial<RelatorioCampanhasResponse> = {},
): RelatorioCampanhasResponse {
  return {
    de: '2026-09-01',
    ate: '2026-09-30',
    linhas: [
      {
        origem: 'meta_lead_ads',
        campanha: { id: 10, nome: 'Setembro | Serviço Y' },
        leadsNovos: 4,
        entradas: 4,
        emAndamento: 2,
        ganhos: 1,
        perdidos: 1,
        taxaConversao: 0.25,
        medianaPrimeiroContatoMinutos: 30,
      },
      {
        origem: 'google_ads',
        campanha: { id: 11, nome: 'Google | Serviço Z' },
        leadsNovos: 2,
        entradas: 3,
        emAndamento: 0,
        ganhos: 1,
        perdidos: 1,
        taxaConversao: 0.5,
        medianaPrimeiroContatoMinutos: 40,
      },
      {
        origem: 'site',
        campanha: null,
        leadsNovos: 1,
        entradas: 2,
        emAndamento: 1,
        ganhos: 0,
        perdidos: 0,
        taxaConversao: 0,
        medianaPrimeiroContatoMinutos: null,
      },
      {
        origem: 'manual',
        campanha: null,
        leadsNovos: 1,
        entradas: 1,
        emAndamento: 1,
        ganhos: 0,
        perdidos: 0,
        taxaConversao: 0,
        medianaPrimeiroContatoMinutos: null,
      },
    ],
    total: {
      leadsNovos: 8,
      entradas: 10,
      emAndamento: 4,
      ganhos: 2,
      perdidos: 2,
      taxaConversao: 0.25,
      medianaPrimeiroContatoMinutos: 30,
    },
    ...parcial,
  }
}

/** Relatório de um período sem nenhum lead novo nem entrada. */
export function relatorioVazio(): RelatorioCampanhasResponse {
  return relatorio({
    linhas: [],
    total: {
      leadsNovos: 0,
      entradas: 0,
      emAndamento: 0,
      ganhos: 0,
      perdidos: 0,
      taxaConversao: null,
      medianaPrimeiroContatoMinutos: null,
    },
  })
}

/** Relatório simulado. Devolve os endereços pedidos, para conferir o período. */
export function comRelatorio(resposta: RelatorioCampanhasResponse = relatorio()) {
  const pedidos: URL[] = []
  servidor.use(
    http.get('*/api/relatorios/campanhas', ({ request }) => {
      pedidos.push(new URL(request.url))
      return HttpResponse.json(resposta)
    }),
  )
  return pedidos
}
