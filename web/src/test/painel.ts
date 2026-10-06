import { http, HttpResponse } from 'msw'
import type { PainelResponse } from '@/api/cliente'
import { atendente } from './api'
import { campanhas, comCadastros, etapas, usuarios } from './leads'
import { servidor } from './servidor'

/** Painel de setembro com os números da massa conhecida da API (MassaDoPainel). */
export function painel(parcial: Partial<PainelResponse> = {}): PainelResponse {
  const leadsPorEtapa = [3, 1, 0, 0, 2, 2]
  return {
    de: '2026-09-01',
    ate: '2026-09-30',
    leadsNovos: 8,
    entradas: 10,
    ganhos: 2,
    taxaConversao: 0.25,
    primeiroContato: { medianaMinutos: 30, comContato: 5, semContato: 3 },
    situacaoAtual: etapas.map((etapa, indice) => ({
      etapa: { id: etapa.id, nome: etapa.nome, tipo: etapa.tipo },
      leadsNovos: leadsPorEtapa[indice],
    })),
    porOrigem: [
      { origem: 'meta_lead_ads', leadsNovos: 4, ganhos: 1, taxaConversao: 0.25 },
      { origem: 'google_ads', leadsNovos: 2, ganhos: 1, taxaConversao: 0.5 },
      { origem: 'site', leadsNovos: 1, ganhos: 0, taxaConversao: 0 },
      { origem: 'manual', leadsNovos: 1, ganhos: 0, taxaConversao: 0 },
    ],
    porCampanha: [
      {
        campanha: { id: campanhas[0].id, nome: campanhas[0].nome },
        leadsNovos: 4,
        ganhos: 1,
        taxaConversao: 0.25,
      },
      {
        campanha: { id: 11, nome: 'Google | Serviço Z' },
        leadsNovos: 2,
        ganhos: 1,
        taxaConversao: 0.5,
      },
      { campanha: null, leadsNovos: 2, ganhos: 0, taxaConversao: 0 },
    ],
    perdas: {
      leads: 3,
      porMotivo: [
        { motivo: { id: 3, nome: 'Preço' }, leads: 1 },
        { motivo: { id: 2, nome: 'Sem interesse' }, leads: 1 },
        { motivo: { id: 1, nome: 'Sem resposta' }, leads: 1 },
      ],
    },
    porAtendente: [
      {
        responsavel: { id: atendente.id, nome: 'Adriana', ativo: true },
        leadsNovos: 5,
        ganhos: 1,
        taxaConversao: 0.2,
        medianaPrimeiroContatoMinutos: 45,
      },
      {
        responsavel: { id: usuarios[2].id, nome: 'Pietra', ativo: false },
        leadsNovos: 2,
        ganhos: 1,
        taxaConversao: 0.5,
        medianaPrimeiroContatoMinutos: 20,
      },
      {
        responsavel: null,
        leadsNovos: 1,
        ganhos: 0,
        taxaConversao: 0,
        medianaPrimeiroContatoMinutos: null,
      },
    ],
    ...parcial,
  }
}

/** Painel de um período sem nenhum lead. */
export function painelVazio(): PainelResponse {
  return painel({
    leadsNovos: 0,
    entradas: 0,
    ganhos: 0,
    taxaConversao: null,
    primeiroContato: { medianaMinutos: null, comContato: 0, semContato: 0 },
    situacaoAtual: painel().situacaoAtual.map((linha) => ({ ...linha, leadsNovos: 0 })),
    porOrigem: [],
    porCampanha: [],
    perdas: { leads: 0, porMotivo: [] },
    porAtendente: [],
  })
}

/** Painel simulado, com os cadastros dos filtros. Devolve os endereços pedidos, para conferir os filtros. */
export function comPainel(resposta: PainelResponse = painel()) {
  const pedidos: URL[] = []
  comCadastros()
  servidor.use(
    http.get('*/api/painel', ({ request }) => {
      pedidos.push(new URL(request.url))
      return HttpResponse.json(resposta)
    }),
  )
  return pedidos
}
