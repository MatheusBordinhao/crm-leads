import type { PainelResponse } from '@/api/cliente'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { formatarDuracao } from '@/lib/datas'
import { formatarNumero, formatarTaxa } from '@/lib/numeros'

type Atendente = NonNullable<PainelResponse['porAtendente']>[number]

function nome({ responsavel }: Atendente) {
  if (!responsavel) {
    return 'Sem responsável'
  }
  return responsavel.ativo ? responsavel.nome : `${responsavel.nome} (desativado)`
}

function primeiroContato({ medianaPrimeiroContatoMinutos: mediana }: Atendente) {
  return mediana === null ? '—' : formatarDuracao(mediana)
}

/** Os números de cada responsável (só admin): tabela no computador e cartões no celular. */
export function TabelaAtendentes({
  atendentes,
  tituloId,
}: {
  atendentes: Atendente[]
  tituloId: string
}) {
  if (atendentes.length === 0) {
    return <p className="text-sm text-muted-foreground">Nenhum lead novo no período.</p>
  }

  return (
    <>
      <div className="hidden md:block">
        <Table aria-labelledby={tituloId}>
          <TableHeader>
            <TableRow>
              <TableHead>Atendente</TableHead>
              <TableHead className="text-right">Leads novos</TableHead>
              <TableHead className="text-right">Ganhos</TableHead>
              <TableHead className="text-right">Conversão</TableHead>
              <TableHead className="text-right">Primeiro contato</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {atendentes.map((atendente) => (
              <TableRow key={atendente.responsavel?.id ?? 'sem-responsavel'}>
                <TableCell>{nome(atendente)}</TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatarNumero(atendente.leadsNovos)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatarNumero(atendente.ganhos)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatarTaxa(atendente.taxaConversao)}
                </TableCell>
                <TableCell className="text-right">{primeiroContato(atendente)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      <ul aria-labelledby={tituloId} className="flex flex-col gap-3 md:hidden">
        {atendentes.map((atendente) => (
          <li
            key={atendente.responsavel?.id ?? 'sem-responsavel'}
            className="rounded-lg border p-3"
          >
            <div className="font-medium">{nome(atendente)}</div>
            <dl className="mt-2 grid grid-cols-[1fr_auto] gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Leads novos</dt>
              <dd className="text-right tabular-nums">{formatarNumero(atendente.leadsNovos)}</dd>
              <dt className="text-muted-foreground">Ganhos</dt>
              <dd className="text-right tabular-nums">{formatarNumero(atendente.ganhos)}</dd>
              <dt className="text-muted-foreground">Conversão</dt>
              <dd className="text-right tabular-nums">{formatarTaxa(atendente.taxaConversao)}</dd>
              <dt className="text-muted-foreground">Primeiro contato</dt>
              <dd className="text-right">{primeiroContato(atendente)}</dd>
            </dl>
          </li>
        ))}
      </ul>
    </>
  )
}
