import { useState } from 'react'
import { useNavigate } from 'react-router'
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
import { useExcluirLead } from './dados'

/** Confirmação da exclusão de um lead de teste, com tudo o que depende dele. Depois, volta para a lista. */
export function ExcluirLeadDialog({
  lead,
  aoFechar,
}: {
  lead: LeadResponse
  aoFechar: () => void
}) {
  const excluir = useExcluirLead(lead.id)
  const navigate = useNavigate()
  const [erro, setErro] = useState<string | null>(null)

  const confirmar = () => {
    setErro(null)
    excluir.mutate(undefined, {
      onSuccess: () => {
        toast.success('Lead de teste excluído.')
        navigate('/leads', { replace: true })
      },
      onError: (falha) => setErro(mensagemDoErro(falha)),
    })
  }

  return (
    <Dialog open onOpenChange={(aberto) => !aberto && aoFechar()}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Excluir o lead de teste {lead.nome}?</DialogTitle>
          <DialogDescription>
            Saem juntos as entradas, as interações e o histórico dele. Isto não tem volta.
          </DialogDescription>
        </DialogHeader>
        {erro && (
          <Alert variant="destructive">
            <AlertDescription>{erro}</AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button type="button" variant="outline" onClick={aoFechar}>
            Cancelar
          </Button>
          <Button
            type="button"
            variant="destructive"
            disabled={excluir.isPending}
            onClick={confirmar}
          >
            {excluir.isPending ? 'Excluindo…' : 'Excluir'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
