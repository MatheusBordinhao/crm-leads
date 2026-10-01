import { http, HttpResponse } from 'msw'
import type { NotificacaoResponse } from '@/api/cliente'
import { servidor } from './servidor'

export function notificacao(parcial: Partial<NotificacaoResponse> = {}): NotificacaoResponse {
  return {
    id: 1,
    tipo: 'lead_atribuido',
    titulo: 'Novo lead para você',
    lead: { id: 1024, nome: 'Maria Souza' },
    criadoEm: new Date(Date.now() - 5 * 60_000).toISOString(),
    lidaEm: null,
    ...parcial,
  }
}

/**
 * Notificações simuladas, com estado: marcar como lida muda a lista recarregada. Devolve as consultas e as
 * marcações recebidas.
 */
export function comNotificacoes(itens: NotificacaoResponse[]) {
  let lista = itens.map((item) => ({ ...item }))
  const consultas: URL[] = []
  const marcadas: string[] = []
  const lida = (item: NotificacaoResponse) => ({
    ...item,
    lidaEm: item.lidaEm ?? new Date().toISOString(),
  })
  servidor.use(
    http.get('*/api/notificacoes', ({ request }) => {
      consultas.push(new URL(request.url))
      return HttpResponse.json({
        naoLidas: lista.filter((item) => !item.lidaEm).length,
        itens: lista,
      })
    }),
    http.post('*/api/notificacoes/:id/lida', ({ params }) => {
      marcadas.push(String(params.id))
      lista = lista.map((item) => (item.id === Number(params.id) ? lida(item) : item))
      return new HttpResponse(null, { status: 204 })
    }),
    http.post('*/api/notificacoes/lidas', () => {
      marcadas.push('todas')
      lista = lista.map(lida)
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return { consultas, marcadas }
}
