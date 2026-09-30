import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { mensagemDoErro } from '@/lib/formularios'

type Props = {
  id: string
  rotulo: string
  textoDoBotao: string
  tamanhoMaximo: number
  /** Cria o cadastro; o campo é limpo quando dá certo e mostra o erro da API quando não dá. */
  aoCriar: (nome: string) => Promise<unknown>
}

/** Campo e botão para acrescentar uma etapa ou um motivo de perda. */
export function NovoNome({ id, rotulo, textoDoBotao, tamanhoMaximo, aoCriar }: Props) {
  const [nome, setNome] = useState('')
  const [erro, setErro] = useState<string | null>(null)
  const [salvando, setSalvando] = useState(false)

  const criar = async (evento: FormEvent) => {
    evento.preventDefault()
    const limpo = nome.trim()
    if (!limpo) {
      setErro('Informe o nome.')
      return
    }
    if (limpo.length > tamanhoMaximo) {
      setErro(`O nome pode ter até ${tamanhoMaximo} caracteres.`)
      return
    }
    setErro(null)
    setSalvando(true)
    try {
      await aoCriar(limpo)
      setNome('')
    } catch (falha) {
      setErro(mensagemDoErro(falha))
    } finally {
      setSalvando(false)
    }
  }

  return (
    <form onSubmit={criar} noValidate className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {rotulo}
      </label>
      <div className="flex gap-2">
        <Input
          id={id}
          value={nome}
          autoComplete="off"
          aria-invalid={erro ? true : undefined}
          aria-describedby={erro ? `${id}-erro` : undefined}
          onChange={(evento) => setNome(evento.target.value)}
        />
        <Button type="submit" variant="secondary" disabled={salvando}>
          {textoDoBotao}
        </Button>
      </div>
      {erro && (
        <p id={`${id}-erro`} className="text-sm text-destructive">
          {erro}
        </p>
      )}
    </form>
  )
}
