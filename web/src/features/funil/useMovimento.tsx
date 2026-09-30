import { useState } from 'react'
import { useNavigate } from 'react-router'
import { toast } from 'sonner'
import type { CartaoFunilResponse, EtapaResumo, FiltrosFunil } from '@/api/cliente'
import { MotivoPerdaDialog, type MotivoEscolhido } from '@/features/leads/MotivoPerdaDialog'
import { mensagemDoErro } from '@/lib/formularios'
import { useMoverNoQuadro } from './dados'

type Pendente = { cartao: CartaoFunilResponse; origemId: number; destino: EtapaResumo }

/**
 * Mover um cartão, arrastando no quadro ou pela lista do celular. Perdido pede o motivo antes: enquanto o
 * pedido está aberto, o cartão sai da coluna, e cancelar o devolve. Uma recusa da API (um Ganho sem CPF/CNPJ,
 * por exemplo) desfaz o movimento e mostra a mensagem, com um atalho para a ficha.
 */
export function useMovimento(filtros: FiltrosFunil) {
  const mover = useMoverNoQuadro(filtros)
  const navigate = useNavigate()
  const [pendente, setPendente] = useState<Pendente | null>(null)
  const [erroDoMotivo, setErroDoMotivo] = useState<string | null>(null)

  const executar = (
    cartao: CartaoFunilResponse,
    origemId: number,
    destino: EtapaResumo,
    motivo?: MotivoEscolhido,
  ) => {
    setErroDoMotivo(null)
    mover.mutate(
      {
        leadId: cartao.id,
        cartao,
        origemId,
        dados: {
          etapaId: destino.id,
          motivoPerdaId: motivo?.motivoPerdaId ?? null,
          motivoPerdaDetalhe: motivo?.motivoPerdaDetalhe ?? null,
        },
      },
      {
        onSuccess: () => setPendente(null),
        onError: (erro) => {
          const mensagem = mensagemDoErro(erro)
          if (motivo) {
            setErroDoMotivo(mensagem)
            return
          }
          toast.error(mensagem, {
            action: { label: 'Abrir a ficha', onClick: () => navigate(`/leads/${cartao.id}`) },
          })
        },
      },
    )
  }

  /** O cartão foi solto (ou escolhido) numa etapa. Na mesma etapa, nada acontece. */
  const soltar = (cartao: CartaoFunilResponse, origemId: number, destino: EtapaResumo) => {
    if (destino.id === origemId) {
      return
    }
    if (destino.tipo === 'perdido') {
      setErroDoMotivo(null)
      setPendente({ cartao, origemId, destino })
      return
    }
    executar(cartao, origemId, destino)
  }

  const dialogo = pendente && (
    <MotivoPerdaDialog
      nomeDoLead={pendente.cartao.nome}
      salvando={mover.isPending}
      erro={erroDoMotivo}
      aoConfirmar={(motivo) =>
        executar(pendente.cartao, pendente.origemId, pendente.destino, motivo)
      }
      aoCancelar={() => setPendente(null)}
    />
  )

  return { soltar, pendenteId: pendente?.cartao.id ?? null, dialogo }
}
