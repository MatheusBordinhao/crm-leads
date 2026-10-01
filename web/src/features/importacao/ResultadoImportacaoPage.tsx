import { useQueryClient } from '@tanstack/react-query'
import { ArrowLeftIcon, DownloadIcon, TriangleAlertIcon } from 'lucide-react'
import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { ErroApi, type ImportacaoResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { chaveFunil, chaveLeads } from '@/features/leads/dados'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { formatarDataHora } from '@/lib/datas'
import { linkDoRelatorioDeErros, useImportacao } from './dados'

/** Andamento e resultado de uma importação. Enquanto ela processa, a tela consulta a API a cada segundo. */
export function ResultadoImportacaoPage() {
  const id = Number(useParams().id)
  const { data: importacao, error, isError, refetch } = useImportacao(id)
  const queryClient = useQueryClient()
  const concluida = importacao?.situacao === 'concluida'

  useEffect(() => {
    if (concluida) {
      // Os leads novos aparecem na lista e no funil sem recarregar a página.
      void queryClient.invalidateQueries({ queryKey: chaveLeads })
      void queryClient.invalidateQueries({ queryKey: chaveFunil })
    }
  }, [concluida, queryClient])

  if (!Number.isSafeInteger(id) || id <= 0 || (error instanceof ErroApi && error.status === 404)) {
    return (
      <section className="flex flex-col items-start gap-3">
        <h1 className="text-xl font-semibold">Importação não encontrada</h1>
        <Button asChild variant="outline">
          <Link to="/importacao">Nova importação</Link>
        </Button>
      </section>
    )
  }
  if (isError) {
    return (
      <div className="flex flex-col items-start gap-2">
        <p>Não foi possível carregar a importação.</p>
        <Button variant="outline" onClick={() => refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }
  if (!importacao) {
    return <Carregando />
  }

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/importacao"
        className="flex items-center gap-1 self-start text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeftIcon className="size-4" aria-hidden />
        Importação
      </Link>
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold break-all">{importacao.nomeArquivo}</h1>
        <p className="text-sm text-muted-foreground">
          {[
            `Importado por ${importacao.importadoPor} em ${formatarDataHora(importacao.criadoEm)}`,
            ROTULOS_ORIGEM[importacao.origem],
            importacao.campanhaPadrao && `Campanha padrão: ${importacao.campanhaPadrao}`,
            importacao.responsavel ? `Para ${importacao.responsavel.nome}` : 'Pelo rodízio',
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
      </div>
      {concluida ? <Resultado importacao={importacao} /> : <Andamento importacao={importacao} />}
    </section>
  )
}

function Andamento({ importacao }: { importacao: ImportacaoResponse }) {
  const { linhasProcessadas, totalLinhas } = importacao
  const porcentagem = totalLinhas === 0 ? 0 : Math.round((linhasProcessadas / totalLinhas) * 100)
  return (
    <Card>
      <CardHeader>
        <CardTitle>Importando…</CardTitle>
        <CardDescription>
          Pode sair desta tela: a importação continua, e o sino avisa quando ela terminar.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <div
          role="progressbar"
          aria-label="Andamento da importação"
          aria-valuemin={0}
          aria-valuemax={totalLinhas}
          aria-valuenow={linhasProcessadas}
          aria-valuetext={`${linhasProcessadas} de ${totalLinhas} linhas`}
          className="h-2 w-full overflow-hidden rounded-full bg-primary/15"
        >
          <div
            className="h-full rounded-full bg-primary transition-[width]"
            style={{ width: `${porcentagem}%` }}
          />
        </div>
        <p className="text-sm text-muted-foreground">
          {linhasProcessadas} de {totalLinhas} linhas
        </p>
      </CardContent>
    </Card>
  )
}

function Resultado({ importacao }: { importacao: ImportacaoResponse }) {
  const numeros = [
    { rotulo: 'Leads novos', valor: importacao.criados },
    { rotulo: 'Vinculados a leads existentes', valor: importacao.vinculados },
    { rotulo: 'Já importados antes', valor: importacao.jaImportados },
    { rotulo: 'Com erro', valor: importacao.comErro },
  ]
  return (
    <>
      <dl className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {numeros.map(({ rotulo, valor }) => (
          <div
            key={rotulo}
            className="flex flex-col gap-1 rounded-xl bg-card p-3 text-card-foreground ring-1 ring-foreground/10"
          >
            <dt className="text-sm text-muted-foreground">{rotulo}</dt>
            <dd className="text-2xl font-semibold">{valor}</dd>
          </div>
        ))}
      </dl>

      {importacao.semResponsavel > 0 && (
        <Alert>
          <TriangleAlertIcon aria-hidden />
          <AlertDescription>
            <span>
              {importacao.semResponsavel === 1
                ? '1 lead novo ficou sem responsável.'
                : `${importacao.semResponsavel} leads novos ficaram sem responsável.`}{' '}
              <Link to="/leads?responsavel=sem" className="underline">
                Ver os leads sem responsável
              </Link>
            </span>
          </AlertDescription>
        </Alert>
      )}

      {importacao.comErro > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Linhas com erro</CardTitle>
            <CardDescription>
              Essas linhas não entraram. A linha é a mesma da planilha, contando o cabeçalho como
              linha 1: corrija no arquivo e importe só elas de novo.
              {importacao.comErro > importacao.erros.length &&
                ` Aqui estão as ${importacao.erros.length} primeiras; o relatório traz todas.`}
            </CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <Button asChild variant="outline" className="self-start">
              <a href={linkDoRelatorioDeErros(importacao.id)} download>
                <DownloadIcon aria-hidden />
                Baixar relatório de erros
              </a>
            </Button>
            <Table aria-label="Linhas com erro">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-20">Linha</TableHead>
                  <TableHead>Motivo</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {importacao.erros.map((erro) => (
                  <TableRow key={`${erro.linha}-${erro.motivo}`}>
                    <TableCell className="tabular-nums">{erro.linha}</TableCell>
                    <TableCell className="whitespace-normal">{erro.motivo}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}

      <div className="flex flex-wrap gap-2">
        <Button asChild>
          <Link to="/leads">Ver os leads</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to="/importacao">Nova importação</Link>
        </Button>
      </div>
    </>
  )
}
