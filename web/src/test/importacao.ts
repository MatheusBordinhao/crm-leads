import { http, HttpResponse } from 'msw'
import type { AnaliseImportacaoResponse, ImportacaoResponse } from '@/api/cliente'
import { servidor } from './servidor'

/** A análise de um arquivo exportado pela Meta, com a sugestão de cada coluna. */
export function analise(
  parcial: Partial<AnaliseImportacaoResponse> = {},
): AnaliseImportacaoResponse {
  return {
    nomeArquivo: 'leads-meta.csv',
    codificacao: 'utf-16',
    separador: 'tab',
    colunas: [
      { nome: 'id', sugestao: 'nao_importar' },
      { nome: 'full_name', sugestao: 'nome' },
      { nome: 'email', sugestao: 'email' },
      { nome: 'phone_number', sugestao: 'telefone' },
      { nome: 'qual_serviço_você_procura?', sugestao: 'resposta_extra' },
    ],
    previa: [
      ['l:1234567890', 'João Conceição', 'joao@example.com', 'p:+5551999998888', 'Manutenção'],
      ['l:1234567891', 'Ana Lúcia', 'ana@example.com', 'p:+5551988887777', ''],
    ],
    totalLinhas: 2,
    colunaIdExterno: 0,
    origem: 'meta_lead_ads',
    ...parcial,
  }
}

export function importacao(parcial: Partial<ImportacaoResponse> = {}): ImportacaoResponse {
  return {
    id: 12,
    nomeArquivo: 'leads-meta.csv',
    codificacao: 'utf-16',
    separador: 'tab',
    origem: 'meta_lead_ads',
    campanhaPadrao: null,
    responsavel: null,
    importadoPor: 'Matheus',
    situacao: 'concluida',
    totalLinhas: 5,
    linhasProcessadas: 5,
    criados: 3,
    vinculados: 1,
    jaImportados: 0,
    comErro: 1,
    semResponsavel: 0,
    erros: [{ linha: 4, motivo: 'A linha não tem o nome.' }],
    criadoEm: '2026-10-05T17:03:00Z',
    concluidoEm: '2026-10-05T17:03:05Z',
    ...parcial,
  }
}

/**
 * API da importação simulada. A consulta devolve as respostas da lista em ordem, repetindo a última (para simular
 * o andamento). Devolve os corpos recebidos na análise e na criação.
 */
export function comImportacao({
  respostaDaAnalise = analise(),
  consultas = [importacao()],
}: {
  respostaDaAnalise?: AnaliseImportacaoResponse
  consultas?: ImportacaoResponse[]
} = {}) {
  const analisados: Record<string, unknown>[] = []
  const criados: Record<string, unknown>[] = []
  let consulta = 0
  servidor.use(
    http.post('*/api/importacoes/analise', async ({ request }) => {
      analisados.push((await request.json()) as Record<string, unknown>)
      return HttpResponse.json(respostaDaAnalise)
    }),
    http.post('*/api/importacoes', async ({ request }) => {
      criados.push((await request.json()) as Record<string, unknown>)
      return HttpResponse.json(consultas[0], { status: 202 })
    }),
    http.get('*/api/importacoes/:id', () =>
      HttpResponse.json(consultas[Math.min(consulta++, consultas.length - 1)]),
    ),
  )
  return { analisados, criados }
}
