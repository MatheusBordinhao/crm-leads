import { CopyIcon } from 'lucide-react'
import { toast } from 'sonner'
import { Button } from '@/components/ui/button'

/** Mostra a senha provisória para o admin copiar e enviar à pessoa. Ela não aparece de novo. */
export function SenhaParaEnviar({ nome, senha }: { nome: string; senha: string }) {
  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(senha)
      toast.success('Senha copiada.')
    } catch {
      toast.error('Não foi possível copiar. Selecione a senha e copie à mão.')
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm">
        Envie a senha provisória para {nome}. Ela não aparece de novo, e o sistema pede uma senha
        nova no primeiro acesso.
      </p>
      <div className="flex items-center gap-2">
        <code
          aria-label="Senha provisória"
          className="flex-1 rounded-md bg-muted px-3 py-2 font-mono text-base select-all"
        >
          {senha}
        </code>
        <Button type="button" variant="outline" onClick={copiar}>
          <CopyIcon aria-hidden />
          Copiar
        </Button>
      </div>
    </div>
  )
}
