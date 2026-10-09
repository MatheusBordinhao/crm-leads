import { useState } from 'react'
import { toast } from 'sonner'
import type { LeadResponse } from '@/api/cliente'
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
import { mensagemDoErro } from '@/lib/formularios'
import { useAnonimizarLead } from './dados'

/**
 * Confirmação da anonimização (seção 3.8), que não tem volta: diz o que sai e o que fica. Com uma importação em
 * andamento, a API responde 409, e a mensagem dela aparece aqui.
 */
export function AnonimizarDialog({ lead, aoFechar }: { lead: LeadResponse; aoFechar: () => void }) {
  const anonimizar = useAnonimizarLead(lead.id)
  const [erro, setErro] = useState<string | null>(null)

  const confirmar = () => {
    setErro(null)
    anonimizar.mutate(undefined, {
      onSuccess: () => {
        toast.success('Lead anonimizado.')
        aoFechar()
      },
      onError: (falha) => setErro(mensagemDoErro(falha)),
    })
  }

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Anonimizar {lead.nome}?</DialogTitle>
          <DialogDescription>Isto não tem volta.</DialogDescription>
        </DialogHeader>
        <div className="flex flex-col gap-3 text-sm">
          <p>
            Saem o nome, o e-mail, o telefone, o CPF/CNPJ, a empresa, as respostas do formulário, o
            que chegou de cada origem, as descrições das interações e o detalhe da perda.
          </p>
          <p>
            Ficam a etapa, as datas, a origem, a campanha e a responsável, para o painel manter os
            números.
          </p>
          <p>Se o titular pediu uma cópia dos dados, exporte antes.</p>
          {erro && (
            <Alert variant="destructive">
              <AlertDescription>{erro}</AlertDescription>
            </Alert>
          )}
        </div>
        <DialogFooter>
          <Button type="button" variant="outline" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={anonimizar.isPending}
            onClick={confirmar}
          >
            {anonimizar.isPending ? 'Anonimizando…' : 'Anonimizar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
