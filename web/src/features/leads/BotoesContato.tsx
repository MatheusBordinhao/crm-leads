import { MailIcon, MessageCircleIcon, NotebookPenIcon, PhoneIcon } from 'lucide-react'
import type { LeadResponse, TipoInteracao } from '@/api/cliente'
import { Button } from '@/components/ui/button'

type Props = {
  lead: LeadResponse
  /** Depois do clique, a ficha abre o registro de interação com o tipo já escolhido. */
  aoContatar: (tipo: TipoInteracao) => void
}

/**
 * WhatsApp, Ligar e E-mail (seção 3.5). Um telefone ou e-mail com aviso de formato não vira botão: o link
 * não funcionaria.
 */
export function BotoesContato({ lead, aoContatar }: Props) {
  const comAviso = (campo: string) => lead.avisos.some((aviso) => aviso.campo === campo)
  const telefone = lead.telefone && !comAviso('telefone') ? lead.telefone : null
  const email = lead.email && !comAviso('email') ? lead.email : null

  return (
    <div className="flex flex-wrap gap-2">
      {telefone && (
        <>
          <Button asChild variant="outline">
            <a
              href={`https://wa.me/${telefone.replace(/\D/g, '')}`}
              target="_blank"
              rel="noreferrer"
              onClick={() => aoContatar('whatsapp')}
            >
              <MessageCircleIcon aria-hidden />
              WhatsApp
            </a>
          </Button>
          <Button asChild variant="outline">
            <a href={`tel:${telefone}`} onClick={() => aoContatar('ligacao')}>
              <PhoneIcon aria-hidden />
              Ligar
            </a>
          </Button>
        </>
      )}
      {email && (
        <Button asChild variant="outline">
          <a href={`mailto:${email}`} onClick={() => aoContatar('email')}>
            <MailIcon aria-hidden />
            E-mail
          </a>
        </Button>
      )}
      <Button variant="secondary" onClick={() => aoContatar('nota')}>
        <NotebookPenIcon aria-hidden />
        Registrar interação
      </Button>
    </div>
  )
}
