import { http, HttpResponse } from 'msw'
import type { RegistroAuditoriaResponse } from '@/api/cliente'
import { admin } from './api'
import { comCadastros } from './leads'
import { servidor } from './servidor'

/** Um registro da auditoria: o admin anonimizou o lead 1024. Os testes mudam o que precisam. */
export function registro(
  parcial: Partial<RegistroAuditoriaResponse> = {},
): RegistroAuditoriaResponse {
  return {
    id: 1,
    ocorridoEm: '2026-10-09T15:00:00Z',
    usuario: { id: admin.id, nome: admin.nome },
    acao: 'lead.anonimizado',
    entidade: 'lead',
    entidadeId: '1024',
    detalhes: { entradas: 1, interacoes: 2, eventos: 3 },
    ip: '177.10.20.30',
    ...parcial,
  }
}

/** Auditoria simulada, com os cadastros que dão os nomes. Devolve os endereços pedidos, para conferir os filtros. */
export function comAuditoria(
  itens: RegistroAuditoriaResponse[] = [registro()],
  total = itens.length,
) {
  const pedidos: URL[] = []
  comCadastros()
  servidor.use(
    http.get('*/api/auditoria', ({ request }) => {
      const url = new URL(request.url)
      pedidos.push(url)
      return HttpResponse.json({
        itens,
        total,
        pagina: Number(url.searchParams.get('pagina') ?? 1),
        tamanho: 50,
      })
    }),
  )
  return pedidos
}
