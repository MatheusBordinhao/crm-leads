import { useNavigate } from 'react-router'
import type { NotificacaoResponse } from '@/api/cliente'
import { useMarcarLida } from './dados'

/**
 * Abre uma notificação, pelo sino ou pela notificação nativa: marca como lida e abre a ficha do lead, o resultado da
 * importação (no resumo, para quem importou) ou a lista de leads (nos leads que uma importação deixou com a pessoa).
 */
export function useAbrirNotificacao() {
  const marcarLida = useMarcarLida()
  const navigate = useNavigate()
  return (notificacao: NotificacaoResponse) => {
    if (!notificacao.lidaEm) {
      marcarLida.mutate(notificacao.id)
    }
    if (notificacao.lead) {
      navigate(`/leads/${notificacao.lead.id}`)
    } else if (notificacao.importacaoId) {
      navigate(`/importacao/${notificacao.importacaoId}`)
    } else if (notificacao.tipo === 'leads_importados') {
      navigate('/leads')
    }
  }
}
