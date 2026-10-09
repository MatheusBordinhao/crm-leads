import { DownloadIcon, ScrollTextIcon, Trash2Icon, UserXIcon } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import type { LeadResponse } from '@/api/cliente'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { formatarDataHora } from '@/lib/datas'
import { AnonimizarDialog } from './AnonimizarDialog'
import { linkDosDadosDoTitular } from './dados'
import { ExcluirLeadDialog } from './ExcluirLeadDialog'

/**
 * Privacidade do lead (LGPD, seção 3.8), só para o admin: exportar os dados para o titular, anonimizar e, num lead de
 * teste, excluir. Tudo entra na auditoria, que abre filtrada pelo lead.
 */
export function PrivacidadeDoLead({ lead, className }: { lead: LeadResponse; className?: string }) {
  const [dialogo, setDialogo] = useState<'anonimizar' | 'excluir' | null>(null)

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>Privacidade</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col items-start gap-3">
        <p className="text-sm text-muted-foreground">
          {lead.anonimizadoEm
            ? `Os dados pessoais foram removidos em ${formatarDataHora(lead.anonimizadoEm)}.`
            : 'Pela LGPD, o titular pode pedir uma cópia dos dados ou a remoção deles.'}
        </p>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline" size="sm">
            <a href={linkDosDadosDoTitular(lead.id)} download>
              <DownloadIcon aria-hidden />
              Exportar dados
            </a>
          </Button>
          {lead.teste ? (
            <Button variant="outline" size="sm" onClick={() => setDialogo('excluir')}>
              <Trash2Icon aria-hidden />
              Excluir lead de teste
            </Button>
          ) : (
            !lead.anonimizadoEm && (
              <Button variant="outline" size="sm" onClick={() => setDialogo('anonimizar')}>
                <UserXIcon aria-hidden />
                Anonimizar
              </Button>
            )
          )}
        </div>
        <Button asChild variant="link" size="sm" className="px-0">
          <Link to={`/auditoria?lead=${lead.id}`}>
            <ScrollTextIcon aria-hidden />
            Ver na auditoria
          </Link>
        </Button>
      </CardContent>
      {dialogo === 'anonimizar' && (
        <AnonimizarDialog lead={lead} aoFechar={() => setDialogo(null)} />
      )}
      {dialogo === 'excluir' && <ExcluirLeadDialog lead={lead} aoFechar={() => setDialogo(null)} />}
    </Card>
  )
}
