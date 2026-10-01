import { useState } from 'react'
import { toast } from 'sonner'
import type { LeadResponse } from '@/api/cliente'
import { Selecao } from '@/components/Selecao'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { useUsuarios } from '@/features/usuarios/dados'
import { mensagemDoErro } from '@/lib/formularios'
import { useTrocarResponsavel } from './dados'

/**
 * Responsável do lead na ficha, só para o admin. A troca avisa a nova responsável e entra na linha do tempo;
 * ela não mexe na vez do rodízio. Lead de teste fica sem responsável.
 */
export function ResponsavelDoLead({ lead, className }: { lead: LeadResponse; className?: string }) {
  const { data: usuarios } = useUsuarios()
  const trocar = useTrocarResponsavel(lead.id)
  const [erro, setErro] = useState<string | null>(null)
  const ativos = usuarios?.filter((usuario) => usuario.ativo) ?? []
  const atualDesativada = lead.responsavel && !ativos.some((u) => u.id === lead.responsavel!.id)

  const escolher = (responsavelId: string) => {
    const escolhida = ativos.find((usuario) => usuario.id === responsavelId)
    if (!escolhida || escolhida.id === lead.responsavel?.id) {
      return
    }
    setErro(null)
    trocar.mutate(escolhida.id, {
      onSuccess: () => toast.success(`Lead passado para ${escolhida.nome}.`),
      onError: (falha) => setErro(mensagemDoErro(falha)),
    })
  }

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Responsável</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {lead.teste ? (
          <p className="text-sm text-muted-foreground">
            Lead de teste fica sem responsável e só aparece para os admins.
          </p>
        ) : (
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="responsavel-do-lead">Passar para</Label>
            {/* Controlada pela responsável atual: se a troca falhar, volta sozinha. */}
            <Selecao
              id="responsavel-do-lead"
              value={lead.responsavel?.id ?? ''}
              disabled={trocar.isPending || ativos.length === 0}
              onChange={(evento) => escolher(evento.target.value)}
            >
              {!lead.responsavel && (
                <option value="" disabled>
                  Sem responsável
                </option>
              )}
              {atualDesativada && (
                <option value={lead.responsavel!.id} disabled>
                  {lead.responsavel!.nome} (desativado)
                </option>
              )}
              {ativos.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
            </Selecao>
          </div>
        )}
        {erro && (
          <Alert variant="destructive">
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}
      </CardContent>
    </Card>
  )
}
