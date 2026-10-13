import { useState } from 'react'
import { toast } from 'sonner'
import type { LeadResponse } from '@/api/cliente'
import { Selecao } from '@/components/Selecao'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { formatarDataHora } from '@/lib/datas'
import { mensagemDoErro } from '@/lib/formularios'
import { useEtapas, useMudarEtapa } from './dados'
import { MotivoPerdaDialog, type MotivoEscolhido } from './MotivoPerdaDialog'
import { SeloEtapa } from './SeloEtapa'

/**
 * Etapa do lead na ficha. Mudar para Perdido pede o motivo antes; um Ganho recusado (sem CPF/CNPJ válido)
 * mostra a mensagem da API, e o lead continua onde estava.
 */
export function EtapaDoLead({ lead, className }: { lead: LeadResponse; className?: string }) {
  const { data: etapas } = useEtapas()
  const mudar = useMudarEtapa()
  const [pedindoMotivo, setPedindoMotivo] = useState<number | null>(null)
  const [erro, setErro] = useState<string | null>(null)
  const ativas = etapas?.filter((etapa) => etapa.ativa) ?? []
  // Enquanto as etapas carregam, a seleção mostra ao menos a atual.
  const opcoes = ativas.some((etapa) => etapa.id === lead.etapa.id)
    ? ativas
    : [lead.etapa, ...ativas]

  const mover = (etapaId: number, motivo?: MotivoEscolhido) => {
    setErro(null)
    mudar.mutate(
      {
        leadId: lead.id,
        dados: {
          etapaId,
          motivoPerdaId: motivo?.motivoPerdaId ?? null,
          motivoPerdaDetalhe: motivo?.motivoPerdaDetalhe ?? null,
        },
      },
      {
        onSuccess: (atualizado) => {
          setPedindoMotivo(null)
          toast.success(`Lead movido para ${atualizado.etapa.nome}.`)
        },
        onError: (falha) => setErro(mensagemDoErro(falha)),
      },
    )
  }

  const escolher = (etapaId: number) => {
    const destino = ativas.find((etapa) => etapa.id === etapaId)
    if (!destino || destino.id === lead.etapa.id) {
      return
    }
    if (destino.tipo === 'perdido') {
      setErro(null)
      setPedindoMotivo(destino.id)
    } else {
      mover(destino.id)
    }
  }

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Etapa</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <SeloEtapa etapa={lead.etapa} />
          {lead.convertidoEm && (
            <span className="text-muted-foreground">
              desde {formatarDataHora(lead.convertidoEm)}
            </span>
          )}
          {lead.perdidoEm && (
            <span className="text-muted-foreground">desde {formatarDataHora(lead.perdidoEm)}</span>
          )}
        </div>
        {lead.motivoPerda && (
          <p className="text-sm break-words">
            Motivo: {lead.motivoPerda.nome}
            {lead.motivoPerdaDetalhe && ` · ${lead.motivoPerdaDetalhe}`}
          </p>
        )}
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="etapa-do-lead">Mudar para</Label>
          {/* Controlado pela etapa atual: se a mudança for cancelada ou recusada, volta sozinho. */}
          <Selecao
            id="etapa-do-lead"
            value={lead.etapa.id}
            disabled={mudar.isPending || ativas.length === 0}
            onChange={(evento) => escolher(Number(evento.target.value))}
          >
            {opcoes.map((etapa) => (
              <option key={etapa.id} value={etapa.id}>
                {etapa.nome}
              </option>
            ))}
          </Selecao>
        </div>
        {erro && pedindoMotivo === null && (
          <Alert variant="destructive">
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}
      </CardContent>
      {pedindoMotivo !== null && (
        <MotivoPerdaDialog
          nomeDoLead={lead.nome}
          anonimizado={lead.anonimizadoEm !== null}
          salvando={mudar.isPending}
          erro={erro}
          aoConfirmar={(motivo) => mover(pedindoMotivo, motivo)}
          aoCancelar={() => {
            setPedindoMotivo(null)
            setErro(null)
          }}
        />
      )}
    </Card>
  )
}
