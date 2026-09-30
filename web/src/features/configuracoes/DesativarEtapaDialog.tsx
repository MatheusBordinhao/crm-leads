import { useState } from 'react'
import { toast } from 'sonner'
import type { EtapaResponse } from '@/api/cliente'
import { Selecao } from '@/components/Selecao'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Label } from '@/components/ui/label'
import { mensagemDoErro } from '@/lib/formularios'
import { useDesativarEtapa } from './dados'

type Props = {
  etapa: EtapaResponse
  /** As outras etapas abertas ativas, que podem receber os leads. */
  destinos: EtapaResponse[]
  aoFechar: () => void
}

/**
 * Desativar uma etapa (seção 3.4). Etapa com leads não pode ser desativada sem destino: a tela já pede para
 * onde eles vão, e a API move todos e desativa na mesma transação, com uma linha no histórico por lead.
 */
export function DesativarEtapaDialog({ etapa, destinos, aoFechar }: Props) {
  const desativar = useDesativarEtapa()
  const [destinoId, setDestinoId] = useState(destinos[0]?.id ?? 0)
  const [erro, setErro] = useState<string | null>(null)

  const confirmar = () => {
    setErro(null)
    desativar.mutate(
      { id: etapa.id, moverLeadsParaId: destinoId || null },
      {
        onSuccess: () => {
          toast.success(`Etapa ${etapa.nome} desativada.`)
          aoFechar()
        },
        onError: (falha) => setErro(mensagemDoErro(falha)),
      },
    )
  }

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Desativar {etapa.nome}?</DialogTitle>
          <DialogDescription>
            A etapa sai do funil. Os leads que estiverem nela vão para a etapa escolhida, e cada
            mudança fica no histórico do lead.
          </DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          {erro && (
            <Alert variant="destructive">
              <AlertDescription>{erro}</AlertDescription>
            </Alert>
          )}
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="destino-dos-leads">Mover os leads para</Label>
            <Selecao
              id="destino-dos-leads"
              value={destinoId}
              onChange={(evento) => setDestinoId(Number(evento.target.value))}
            >
              {destinos.map((destino) => (
                <option key={destino.id} value={destino.id}>
                  {destino.nome}
                </option>
              ))}
            </Selecao>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={aoFechar}>
              Cancelar
            </Button>
            <Button variant="destructive" disabled={desativar.isPending} onClick={confirmar}>
              {desativar.isPending ? 'Desativando…' : 'Desativar'}
            </Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  )
}
