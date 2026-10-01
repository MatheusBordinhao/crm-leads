import { useState } from 'react'
import type { AnaliseImportacaoResponse } from '@/api/cliente'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { mensagemDoErro } from '@/lib/formularios'
import { lerEmBase64, MENSAGEM_TAMANHO, TAMANHO_MAXIMO } from './arquivo'
import { useAnalisarArquivo } from './dados'
import { FormularioImportacao } from './FormularioImportacao'
import { ROTULOS_CODIFICACAO, ROTULOS_SEPARADOR } from './rotulos'

type ArquivoAnalisado = { nome: string; conteudoBase64: string; analise: AnaliseImportacaoResponse }

/**
 * Importação de leads por CSV (seção 3.7), só para admin. O arquivo é lido assim que escolhido, e a tela mostra a
 * prévia, o campo sugerido para cada coluna e as opções antes de importar.
 */
export function ImportacaoPage() {
  const analisar = useAnalisarArquivo()
  const [arquivo, setArquivo] = useState<ArquivoAnalisado | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const escolher = async (escolhido: File | undefined) => {
    setErro(null)
    setArquivo(null)
    if (!escolhido) {
      return
    }
    if (escolhido.size > TAMANHO_MAXIMO) {
      setErro(MENSAGEM_TAMANHO)
      return
    }

    let conteudoBase64: string
    try {
      conteudoBase64 = await lerEmBase64(escolhido)
    } catch {
      setErro('Não foi possível ler o arquivo.')
      return
    }
    analisar.mutate(
      { nomeArquivo: escolhido.name, conteudoBase64 },
      {
        onSuccess: (analise) => setArquivo({ nome: escolhido.name, conteudoBase64, analise }),
        onError: (falha) => setErro(mensagemDoErro(falha)),
      },
    )
  }

  const analise = arquivo?.analise
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Importação de leads</h1>

      <Card>
        <CardHeader>
          <CardTitle>Arquivo</CardTitle>
          <CardDescription>
            CSV de até 5 MB e 5.000 linhas, como o exportado pela Meta ou o salvo pelo Excel
            (Arquivo › Salvar como › CSV). A primeira linha precisa ser o cabeçalho.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Field data-invalid={erro ? true : undefined}>
            <FieldLabel htmlFor="arquivo">Arquivo CSV</FieldLabel>
            <Input
              id="arquivo"
              type="file"
              accept=".csv,.txt,.tsv,text/csv,text/plain,text/tab-separated-values"
              aria-invalid={erro ? true : undefined}
              disabled={analisar.isPending}
              onChange={(evento) => void escolher(evento.target.files?.[0])}
            />
            {analisar.isPending && <FieldDescription>Lendo o arquivo…</FieldDescription>}
            {analise && (
              <FieldDescription>
                {[
                  ROTULOS_CODIFICACAO[analise.codificacao],
                  ROTULOS_SEPARADOR[analise.separador],
                  analise.totalLinhas === 1 ? '1 linha' : `${analise.totalLinhas} linhas`,
                ].join(' · ')}
              </FieldDescription>
            )}
            <FieldError>{erro}</FieldError>
          </Field>
        </CardContent>
      </Card>

      {arquivo && (
        <FormularioImportacao
          arquivo={{ nome: arquivo.nome, conteudoBase64: arquivo.conteudoBase64 }}
          analise={arquivo.analise}
        />
      )}
    </section>
  )
}
