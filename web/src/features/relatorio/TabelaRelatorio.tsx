import { Fragment } from 'react'
import type { LinhaRelatorioResponse, RelatorioCampanhasResponse } from '@/api/cliente'
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { formatarDuracao } from '@/lib/datas'
import { formatarNumero, formatarTaxa } from '@/lib/numeros'

/** O rótulo das linhas sem campanha, o mesmo do painel: são leads que não vieram de anúncio. */
const SEM_CAMPANHA = 'Sem campanha (não veio de anúncio)'

type Numeros = RelatorioCampanhasResponse['total']

/** As colunas de números, na ordem do CSV. */
const COLUNAS: { rotulo: string; valor: (numeros: Numeros) => string }[] = [
  { rotulo: 'Leads novos', valor: (n) => formatarNumero(n.leadsNovos) },
  { rotulo: 'Entradas', valor: (n) => formatarNumero(n.entradas) },
  { rotulo: 'Em andamento', valor: (n) => formatarNumero(n.emAndamento) },
  { rotulo: 'Ganhos', valor: (n) => formatarNumero(n.ganhos) },
  { rotulo: 'Perdidos', valor: (n) => formatarNumero(n.perdidos) },
  { rotulo: 'Conversão', valor: (n) => formatarTaxa(n.taxaConversao) },
  {
    rotulo: 'Primeiro contato',
    valor: (n) =>
      n.medianaPrimeiroContatoMinutos === null
        ? '—'
        : formatarDuracao(n.medianaPrimeiroContatoMinutos),
  },
]

function nome(linha: LinhaRelatorioResponse) {
  return linha.campanha?.nome ?? SEM_CAMPANHA
}

function chave(linha: LinhaRelatorioResponse) {
  return linha.campanha ? `campanha-${linha.campanha.id}` : `origem-${linha.origem}`
}

/**
 * As linhas do relatório e o total: tabela no computador e cartões no celular. Os leads sem campanha vêm no fim, uma
 * linha por origem, como a API manda.
 */
export function TabelaRelatorio({ relatorio }: { relatorio: RelatorioCampanhasResponse }) {
  const { linhas, total } = relatorio
  if (linhas.length === 0) {
    return (
      <p className="text-sm text-muted-foreground">
        Nenhuma campanha teve leads novos ou entradas no período.
      </p>
    )
  }

  const cartoes = [
    ...linhas.map((linha) => ({
      chave: chave(linha),
      titulo: nome(linha),
      origem: ROTULOS_ORIGEM[linha.origem],
      numeros: linha,
    })),
    { chave: 'total', titulo: 'Total', origem: undefined, numeros: total },
  ]

  return (
    <>
      <div className="hidden lg:block">
        <Table aria-label="Relatório por campanha">
          <TableHeader>
            <TableRow>
              <TableHead>Campanha</TableHead>
              {COLUNAS.map(({ rotulo }) => (
                <TableHead key={rotulo} className="text-right">
                  {rotulo}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {linhas.map((linha) => (
              <TableRow key={chave(linha)}>
                <TableCell className="whitespace-normal">
                  <div>{nome(linha)}</div>{' '}
                  <div className="text-xs text-muted-foreground">
                    {ROTULOS_ORIGEM[linha.origem]}
                  </div>
                </TableCell>
                {COLUNAS.map(({ rotulo, valor }) => (
                  <TableCell key={rotulo} className="text-right tabular-nums">
                    {valor(linha)}
                  </TableCell>
                ))}
              </TableRow>
            ))}
          </TableBody>
          <TableFooter>
            <TableRow>
              <TableCell>Total</TableCell>
              {COLUNAS.map(({ rotulo, valor }) => (
                <TableCell key={rotulo} className="text-right tabular-nums">
                  {valor(total)}
                </TableCell>
              ))}
            </TableRow>
          </TableFooter>
        </Table>
      </div>

      <ul aria-label="Relatório por campanha" className="flex flex-col gap-3 lg:hidden">
        {cartoes.map((cartao) => (
          <li key={cartao.chave} className="rounded-lg border p-3">
            <div className="font-medium break-words">{cartao.titulo}</div>
            {cartao.origem && <div className="text-xs text-muted-foreground">{cartao.origem}</div>}
            <dl className="mt-2 grid grid-cols-[1fr_auto] gap-x-3 gap-y-1 text-sm">
              {COLUNAS.map(({ rotulo, valor }) => (
                <Fragment key={rotulo}>
                  <dt className="text-muted-foreground">{rotulo}</dt>
                  <dd className="text-right tabular-nums">{valor(cartao.numeros)}</dd>
                </Fragment>
              ))}
            </dl>
          </li>
        ))}
      </ul>
    </>
  )
}
