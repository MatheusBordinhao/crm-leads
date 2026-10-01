import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { useNavigate } from 'react-router'
import type { AnaliseImportacaoResponse } from '@/api/cliente'
import { CampoSelecao } from '@/components/CampoSelecao'
import { CampoTexto } from '@/components/CampoTexto'
import { Selecao } from '@/components/Selecao'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useCampanhas } from '@/features/leads/dados'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { useUsuarios } from '@/features/usuarios/dados'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { useCriarImportacao } from './dados'
import { esquema, escolhasIniciais, NOVA_CAMPANHA, paraApi } from './esquema'
import { GRUPOS_DE_CAMPOS, ORIGENS_DA_IMPORTACAO, ROTULOS_CAMPO } from './rotulos'

type Props = {
  arquivo: { nome: string; conteudoBase64: string }
  analise: AnaliseImportacaoResponse
}

/** Prévia, campo de cada coluna e opções da importação, com a sugestão da análise já escolhida. */
export function FormularioImportacao({ arquivo, analise }: Props) {
  const { data: campanhas } = useCampanhas()
  const { data: usuarios } = useUsuarios()
  const criar = useCriarImportacao()
  const navigate = useNavigate()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    control,
    setValue,
    setError,
    formState: { errors },
  } = useForm({ resolver: zodResolver(esquema), defaultValues: escolhasIniciais(analise) })

  const origem = useWatch({ control, name: 'origem' })
  const campanha = useWatch({ control, name: 'campanha' })
  const campanhasDaOrigem = campanhas?.filter((c) => c.origem === origem) ?? []

  const importar = handleSubmit((escolhas) => {
    setErroGeral(null)
    criar.mutate(paraApi(escolhas, arquivo), {
      onSuccess: (importacao) => navigate(`/importacao/${importacao.id}`),
      onError: (erro) =>
        setErroGeral(
          aplicarErrosDaApi(erro, setError, [
            'campos',
            'colunaIdExterno',
            'origem',
            'novaCampanha',
            'responsavelId',
          ]),
        ),
    })
  })

  // O da tela fica em root.mapeamento; o da API, em "campos".
  const erroDoMapeamento = errors.root?.mapeamento?.message ?? errors.campos?.message
  const total = analise.totalLinhas
  return (
    <form onSubmit={importar} noValidate className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle>Prévia</CardTitle>
          <CardDescription>
            {analise.previa.length === 1
              ? 'A primeira linha do arquivo.'
              : `As ${analise.previa.length} primeiras linhas do arquivo.`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table aria-label="Prévia do arquivo">
            <TableHeader>
              <TableRow>
                {analise.colunas.map((coluna) => (
                  <TableHead key={coluna.nome}>{coluna.nome}</TableHead>
                ))}
              </TableRow>
            </TableHeader>
            <TableBody>
              {analise.previa.map((linha, indice) => (
                <TableRow key={indice}>
                  {linha.map((valor, coluna) => (
                    <TableCell key={coluna} className="max-w-56 truncate" title={valor}>
                      {valor}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Colunas</CardTitle>
          <CardDescription>
            Para onde vai cada coluna. A sugestão veio do cabeçalho: confira e mude o que precisar.
            O nome aceita mais de uma coluna (nome e sobrenome); os outros campos, uma só.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {erroDoMapeamento && (
            <Alert variant="destructive">
              <AlertDescription>{erroDoMapeamento}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {analise.colunas.map((coluna, indice) => {
              const erro = errors.campos?.[indice]
              const exemplo = analise.previa.map((linha) => linha[indice]?.trim()).find(Boolean)
              return (
                <Field key={coluna.nome} data-invalid={erro ? true : undefined}>
                  <FieldLabel htmlFor={`campo-${indice}`} className="break-all">
                    {coluna.nome}
                  </FieldLabel>
                  <Selecao
                    id={`campo-${indice}`}
                    aria-invalid={erro ? true : undefined}
                    {...register(`campos.${indice}`)}
                  >
                    {GRUPOS_DE_CAMPOS.map((grupo) => (
                      <optgroup key={grupo.rotulo} label={grupo.rotulo}>
                        {grupo.campos.map((campo) => (
                          <option key={campo} value={campo}>
                            {ROTULOS_CAMPO[campo]}
                          </option>
                        ))}
                      </optgroup>
                    ))}
                  </Selecao>
                  <FieldDescription className="truncate" title={exemplo}>
                    {exemplo ? `Exemplo: ${exemplo}` : 'Vazia nas primeiras linhas'}
                  </FieldDescription>
                  <FieldError errors={[erro]} />
                </Field>
              )
            })}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Opções</CardTitle>
        </CardHeader>
        <CardContent className="grid gap-5 md:grid-cols-2">
          <CampoSelecao
            id="origem"
            rotulo="Origem"
            descricao="De onde vieram esses leads. O arquivo exportado pela Meta é da origem Meta."
            registro={register('origem', { onChange: () => setValue('campanha', '') })}
            erro={errors.origem}
          >
            {ORIGENS_DA_IMPORTACAO.map((opcao) => (
              <option key={opcao} value={opcao}>
                {ROTULOS_ORIGEM[opcao]}
              </option>
            ))}
          </CampoSelecao>
          <CampoSelecao
            id="colunaIdExterno"
            rotulo="Coluna de id externo"
            descricao="O id do lead na origem, como o da Meta. Sem ela, o sistema reconhece cada linha pelo conteúdo, e reimportar o arquivo também não duplica. Não use o número da linha de uma planilha."
            registro={register('colunaIdExterno')}
            erro={errors.colunaIdExterno}
          >
            <option value="">Nenhuma</option>
            {analise.colunas.map((coluna, indice) => (
              <option key={coluna.nome} value={indice}>
                {coluna.nome}
              </option>
            ))}
          </CampoSelecao>
          <CampoSelecao
            id="campanha"
            rotulo="Campanha padrão"
            descricao="Para as linhas sem campanha."
            registro={register('campanha')}
          >
            <option value="">Nenhuma</option>
            {campanhasDaOrigem.map((opcao) => (
              <option key={opcao.id} value={opcao.id}>
                {opcao.nome}
              </option>
            ))}
            <option value={NOVA_CAMPANHA}>Nova campanha…</option>
          </CampoSelecao>
          {campanha === NOVA_CAMPANHA && (
            <CampoTexto
              id="novaCampanha"
              rotulo="Nome da campanha nova"
              registro={register('novaCampanha')}
              erro={errors.novaCampanha}
            />
          )}
          <CampoSelecao
            id="responsavelId"
            rotulo="Distribuição"
            descricao="No rodízio, cada lead novo vai para a próxima pessoa da vez. Quem já era lead continua com a mesma responsável."
            registro={register('responsavelId')}
            erro={errors.responsavelId}
          >
            <option value="">Rodízio</option>
            {usuarios
              ?.filter((usuario) => usuario.ativo)
              .map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.nome}
                </option>
              ))}
          </CampoSelecao>
        </CardContent>
      </Card>

      {erroGeral && (
        <Alert variant="destructive">
          <AlertDescription>{erroGeral}</AlertDescription>
        </Alert>
      )}
      <div className="flex justify-end">
        <Button type="submit" disabled={criar.isPending}>
          {criar.isPending ? 'Enviando…' : `Importar ${total} ${total === 1 ? 'linha' : 'linhas'}`}
        </Button>
      </div>
    </form>
  )
}
