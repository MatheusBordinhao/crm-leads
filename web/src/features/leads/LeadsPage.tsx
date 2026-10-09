import { DownloadIcon, ListFilterIcon, PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import type { FiltrosLeads, LeadResumoResponse, OrdemLeads, Origem } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Paginacao } from '@/components/Paginacao'
import { Selecao } from '@/components/Selecao'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useEu } from '@/features/acesso/sessao'
import { useUsuarios } from '@/features/usuarios/dados'
import { formatarDataHora } from '@/lib/datas'
import { cn } from '@/lib/utils'
import { linkDaExportacao, useCampanhas, useEtapas, useLeads } from './dados'
import { ORIGENS, ROTULOS_ORIGEM } from './rotulos'
import { SeloEtapa } from './SeloEtapa'

const ORDENS: Record<OrdemLeads, string> = {
  Recentes: 'Mais recentes',
  Antigos: 'Mais antigos',
  Nome: 'Nome',
}

/** Valor do filtro de responsável para os leads que ainda não têm. */
const SEM_RESPONSAVEL = 'sem'

type Mudar = (nome: string, valor: string) => void

/**
 * Filtros guardados no endereço (`/leads?etapa=2&origem=google_ads`): voltar da ficha mantém a lista como
 * estava, e o endereço pode ser compartilhado.
 */
function lerFiltros(params: URLSearchParams): FiltrosLeads {
  const numero = (nome: string) => {
    const valor = params.get(nome)
    return valor && /^\d+$/.test(valor) ? Number(valor) : undefined
  }
  const origem = params.get('origem')
  const ordem = params.get('ordem')
  const responsavel = params.get('responsavel')
  return {
    busca: params.get('busca') || undefined,
    etapaId: numero('etapa'),
    origem: ORIGENS.includes(origem as Origem) ? (origem as Origem) : undefined,
    campanhaId: numero('campanha'),
    responsavelId: responsavel && responsavel !== SEM_RESPONSAVEL ? responsavel : undefined,
    semResponsavel: responsavel === SEM_RESPONSAVEL || undefined,
    de: params.get('de') || undefined,
    ate: params.get('ate') || undefined,
    ordem: ordem && ordem in ORDENS ? (ordem as OrdemLeads) : undefined,
    pagina: numero('pagina'),
  }
}

export function LeadsPage() {
  const { data: eu } = useEu()
  const ehAdmin = eu?.perfil === 'Admin'
  const [params, setParams] = useSearchParams()
  const filtros = lerFiltros(params)
  const { data, isPending, isError, refetch } = useLeads(filtros)

  /** Muda um filtro e volta para a primeira página. */
  const mudar: Mudar = (nome, valor) => {
    const novos = new URLSearchParams(params)
    if (valor) {
      novos.set(nome, valor)
    } else {
      novos.delete(nome)
    }
    if (nome !== 'pagina') {
      novos.delete('pagina')
    }
    setParams(novos, { replace: true })
  }

  const limpar = () => setParams(new URLSearchParams(), { replace: true })

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Leads</h1>
        <div className="flex flex-wrap gap-2">
          {/* Com os filtros da tela e sem a página; a exportação entra na auditoria. */}
          {ehAdmin && (
            <Button asChild variant="outline">
              <a href={linkDaExportacao(filtros)} download>
                <DownloadIcon aria-hidden />
                Exportar CSV
              </a>
            </Button>
          )}
          <Button asChild>
            <Link to="/leads/novo">
              <PlusIcon aria-hidden />
              Novo lead
            </Link>
          </Button>
        </div>
      </div>

      <Filtros params={params} mudar={mudar} limpar={limpar} ehAdmin={ehAdmin} />

      {isPending && <Carregando />}
      {isError && (
        <div className="flex flex-col items-start gap-2">
          <p>Não foi possível carregar os leads.</p>
          <Button variant="outline" onClick={() => refetch()}>
            Tentar de novo
          </Button>
        </div>
      )}

      {data && (
        <>
          <p className="text-sm text-muted-foreground" aria-live="polite">
            {data.total === 1 ? '1 lead' : `${data.total} leads`}
          </p>

          {data.itens.length === 0 ? (
            <p className="py-8 text-center text-muted-foreground">Nenhum lead encontrado.</p>
          ) : (
            <>
              <TabelaLeads leads={data.itens} ehAdmin={ehAdmin} />
              <CartoesLeads leads={data.itens} ehAdmin={ehAdmin} />
            </>
          )}

          <Paginacao
            pagina={data.pagina}
            total={data.total}
            tamanho={data.tamanho}
            irPara={(pagina) => mudar('pagina', String(pagina))}
          />
        </>
      )}
    </section>
  )
}

function Filtros({
  params,
  mudar,
  limpar,
  ehAdmin,
}: {
  params: URLSearchParams
  mudar: Mudar
  limpar: () => void
  ehAdmin: boolean
}) {
  const { data: etapas } = useEtapas()
  const { data: campanhas } = useCampanhas()
  const { data: usuarios } = useUsuarios({ enabled: ehAdmin })
  const [texto, setTexto] = useState(params.get('busca') ?? '')
  const [abertos, setAbertos] = useState(false)

  const buscar = (evento: FormEvent) => {
    evento.preventDefault()
    mudar('busca', texto.trim())
  }

  const limparTudo = () => {
    setTexto('')
    limpar()
  }

  /** Seleção ligada a um parâmetro do endereço. */
  const selecao = (nome: string, rotulo: string, opcoes: ReactNode) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`filtro-${nome}`}>{rotulo}</Label>
      <Selecao
        id={`filtro-${nome}`}
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar(nome, evento.target.value)}
      >
        {opcoes}
      </Selecao>
    </div>
  )

  const data = (nome: string, rotulo: string) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`filtro-${nome}`}>{rotulo}</Label>
      <Input
        id={`filtro-${nome}`}
        type="date"
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar(nome, evento.target.value)}
      />
    </div>
  )

  return (
    <div className="flex flex-col gap-3">
      <form role="search" onSubmit={buscar} className="flex gap-2">
        <Input
          type="search"
          aria-label="Buscar leads"
          placeholder="Nome, e-mail, telefone, empresa ou CPF/CNPJ"
          value={texto}
          onChange={(evento) => setTexto(evento.target.value)}
        />
        <Button type="submit" variant="secondary">
          <SearchIcon aria-hidden />
          <span className="sr-only md:not-sr-only">Buscar</span>
        </Button>
        <Button
          type="button"
          variant="outline"
          className="md:hidden"
          aria-expanded={abertos}
          aria-controls="filtros-leads"
          onClick={() => setAbertos(!abertos)}
        >
          <ListFilterIcon aria-hidden />
          Filtros
        </Button>
      </form>

      <div
        id="filtros-leads"
        className={cn('grid gap-3 sm:grid-cols-2 lg:grid-cols-4', !abertos && 'hidden md:grid')}
      >
        {selecao(
          'etapa',
          'Etapa',
          <>
            <option value="">Todas</option>
            {etapas
              ?.filter((etapa) => etapa.ativa)
              .map((etapa) => (
                <option key={etapa.id} value={etapa.id}>
                  {etapa.nome}
                </option>
              ))}
          </>,
        )}
        {selecao(
          'origem',
          'Origem',
          <>
            <option value="">Todas</option>
            {ORIGENS.map((origem) => (
              <option key={origem} value={origem}>
                {ROTULOS_ORIGEM[origem]}
              </option>
            ))}
          </>,
        )}
        {selecao(
          'campanha',
          'Campanha',
          <>
            <option value="">Todas</option>
            {campanhas?.map((campanha) => (
              <option key={campanha.id} value={campanha.id}>
                {campanha.nome}
              </option>
            ))}
          </>,
        )}
        {ehAdmin &&
          selecao(
            'responsavel',
            'Responsável',
            <>
              <option value="">Todos</option>
              <option value={SEM_RESPONSAVEL}>Sem responsável</option>
              {usuarios?.map((usuario) => (
                <option key={usuario.id} value={usuario.id}>
                  {usuario.ativo ? usuario.nome : `${usuario.nome} (desativado)`}
                </option>
              ))}
            </>,
          )}
        {data('de', 'Entrada a partir de')}
        {data('ate', 'Entrada até')}
        {selecao(
          'ordem',
          'Ordem',
          Object.entries(ORDENS).map(([valor, rotulo]) => (
            <option key={valor} value={valor === 'Recentes' ? '' : valor}>
              {rotulo}
            </option>
          )),
        )}
      </div>

      {params.toString() !== '' && (
        <Button variant="link" className="self-start px-0" onClick={limparTudo}>
          Limpar filtros
        </Button>
      )}
    </div>
  )
}

type PropsLista = { leads: LeadResumoResponse[]; ehAdmin: boolean }

function TabelaLeads({ leads, ehAdmin }: PropsLista) {
  return (
    <div className="hidden rounded-lg border md:block">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Nome</TableHead>
            <TableHead>Contato</TableHead>
            <TableHead>Etapa</TableHead>
            <TableHead>Origem</TableHead>
            {ehAdmin && <TableHead>Responsável</TableHead>}
            <TableHead>Entrada</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {leads.map((lead) => (
            <TableRow key={lead.id}>
              <TableCell>
                <div className="flex items-center gap-2">
                  <Link to={`/leads/${lead.id}`} className="font-medium hover:underline">
                    {lead.nome}
                  </Link>
                  {lead.teste && <Badge variant="outline">Teste</Badge>}
                </div>
                {lead.empresa && <div className="text-muted-foreground">{lead.empresa}</div>}
              </TableCell>
              <TableCell>
                {lead.telefone && <div>{lead.telefone}</div>}
                {lead.email && <div className="text-muted-foreground">{lead.email}</div>}
              </TableCell>
              <TableCell>
                <SeloEtapa etapa={lead.etapa} />
              </TableCell>
              <TableCell>
                <div>{ROTULOS_ORIGEM[lead.origem]}</div>
                {lead.campanha && <div className="text-muted-foreground">{lead.campanha.nome}</div>}
              </TableCell>
              {ehAdmin && (
                <TableCell>
                  {lead.responsavel?.nome ?? (
                    <span className="text-muted-foreground">Sem responsável</span>
                  )}
                </TableCell>
              )}
              <TableCell className="whitespace-nowrap">{formatarDataHora(lead.criadoEm)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function CartoesLeads({ leads, ehAdmin }: PropsLista) {
  return (
    <ul aria-label="Leads" className="flex flex-col gap-3 md:hidden">
      {leads.map((lead) => (
        <li key={lead.id}>
          <Link to={`/leads/${lead.id}`} className="block rounded-xl">
            <Card className="transition-colors hover:bg-accent/50">
              <CardContent className="flex flex-col gap-2">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="truncate font-medium">{lead.nome}</div>
                    {lead.empresa && (
                      <div className="truncate text-sm text-muted-foreground">{lead.empresa}</div>
                    )}
                  </div>
                  <div className="flex shrink-0 flex-col items-end gap-1">
                    <SeloEtapa etapa={lead.etapa} />
                    {lead.teste && <Badge variant="outline">Teste</Badge>}
                  </div>
                </div>
                <div className="truncate text-sm">{lead.telefone ?? lead.email}</div>
                <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
                  <span>{ROTULOS_ORIGEM[lead.origem]}</span>
                  <span>{formatarDataHora(lead.criadoEm)}</span>
                  {ehAdmin && <span>{lead.responsavel?.nome ?? 'Sem responsável'}</span>}
                </div>
              </CardContent>
            </Card>
          </Link>
        </li>
      ))}
    </ul>
  )
}
