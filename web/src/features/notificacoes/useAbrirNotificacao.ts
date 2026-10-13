import { useNavigate } from 'react-router'
import type { NotificacaoResponse } from '@/api/cliente'
import { useMarcarLida } from './dados'

/**
 * O endereço que a notificação abre: a ficha do lead, o resultado da importação (no resumo, para quem importou) ou a
 * lista de leads (nos leads que uma importação deixou com a pessoa).
 */
export function enderecoDaNotificacao(notificacao: NotificacaoResponse): string | null {
  if (notificacao.lead) {
    return `/leads/${notificacao.lead.id}`
  }
  if (notificacao.importacaoId) {
    return `/importacao/${notificacao.importacaoId}`
  }
  return notificacao.tipo === 'leads_importados' ? '/leads' : null
}

/**
 * Abre uma notificação, pelo sino ou pela notificação nativa: marca como lida e vai para o endereço dela
 * ({@link enderecoDaNotificacao}).
 */
export function useAbrirNotificacao() {
  const marcarLida = useMarcarLida()
  const navigate = useNavigate()
  return (notificacao: NotificacaoResponse) => {
    if (!notificacao.lidaEm) {
      marcarLida.mutate(notificacao.id)
    }
    const endereco = enderecoDaNotificacao(notificacao)
    if (endereco) {
      navigate(endereco)
    }
  }
}
