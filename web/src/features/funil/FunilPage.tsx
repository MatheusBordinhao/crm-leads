import { ListFilterIcon, PlusIcon, SearchIcon } from 'lucide-react'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import type { FiltrosFunil, Origem } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Selecao } from '@/components/Selecao'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useEu } from '@/features/acesso/sessao'
import { useCampanhas } from '@/features/leads/dados'
import { ORIGENS, ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { useUsuarios } from '@/features/usuarios/dados'
import { cn } from '@/lib/utils'
import { useQuadro } from './dados'
import { ListaPorEtapa } from './ListaPorEtapa'
import { QuadroFunil } from './QuadroFunil'

/** Valor do filtro de responsável para os leads que ainda não têm, como na lista de leads. */
const SEM_RESPONSAVEL = 'sem'

/** Filtros guardados no endereço, com os mesmos nomes da lista de leads (`/funil?origem=google_ads`). */
function lerFiltros(params: URLSearchParams): FiltrosFunil {
  const origem = params.get('origem')
  const campanha = params.get('campanha')
  const responsavel = params.get('responsavel')
  return {
    busca: params.get('busca') || undefined,
    origem: ORIGENS.includes(origem as Origem) ? (origem as Origem) : undefined,
    campanhaId: campanha && /^\d+$/.test(campanha) ? Number(campanha) : undefined,
    responsavelId: responsavel && responsavel !== SEM_RESPONSAVEL ? responsavel : undefined,
    semResponsavel: responsavel === SEM_RESPONSAVEL || undefined,
  }
}

/** O funil: quadro com arrastar e soltar no computador e lista por etapa no celular. */
export function FunilPage() {
  const { data: eu } = useEu()
  const ehAdmin = eu?.perfil === 'Admin'
  const [params, setParams] = useSearchParams()
  const filtros = lerFiltros(params)
  const { data: quadro, isPending, isError, refetch } = useQuadro(filtros)

  const mudar = (nome: string, valor: string) => {
    const novos = new URLSearchParams(params)
    if (valor) {
      novos.set(nome, valor)
    } else {
      novos.delete(nome)
    }
    setParams(novos, { replace: true })
  }

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Funil</h1>
        <Button asChild>
          <Link to="/leads/novo">
            <PlusIcon aria-hidden />
            Novo lead
          </Link>
        </Button>
      </div>

      <Filtros
        params={params}
        mudar={mudar}
        limpar={() => setParams(new URLSearchParams(), { replace: true })}
        ehAdmin={ehAdmin}
      />

      {isPending && <Carregando />}
      {isError && (
        <div className="flex flex-col items-start gap-2">
          <p>Não foi possível carregar o funil.</p>
          <Button variant="outline" onClick={() => refetch()}>
            Tentar de novo
          </Button>
        </div>
      )}
      {quadro && (
        <>
          <QuadroFunil quadro={quadro} filtros={filtros} mostrarResponsavel={ehAdmin} />
          <ListaPorEtapa quadro={quadro} filtros={filtros} mostrarResponsavel={ehAdmin} />
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
  mudar: (nome: string, valor: string) => void
  limpar: () => void
  ehAdmin: boolean
}) {
  const { data: campanhas } = useCampanhas()
  const { data: usuarios } = useUsuarios({ enabled: ehAdmin })
  const [texto, setTexto] = useState(params.get('busca') ?? '')
  const [abertos, setAbertos] = useState(false)

  const buscar = (evento: FormEvent) => {
    evento.preventDefault()
    mudar('busca', texto.trim())
  }

  const selecao = (nome: string, rotulo: string, opcoes: ReactNode) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`funil-${nome}`}>{rotulo}</Label>
      <Selecao
        id={`funil-${nome}`}
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar(nome, evento.target.value)}
      >
        {opcoes}
      </Selecao>
    </div>
  )

  return (
    <div className="flex flex-col gap-3">
      <form role="search" onSubmit={buscar} className="flex gap-2">
        <Input
          type="search"
          id="busca-funil"
          name="busca"
          aria-label="Buscar no funil"
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
          aria-controls="filtros-funil"
          onClick={() => setAbertos(!abertos)}
        >
          <ListFilterIcon aria-hidden />
          Filtros
        </Button>
      </form>

      <div
        id="filtros-funil"
        className={cn('grid gap-3 sm:grid-cols-2 lg:grid-cols-3', !abertos && 'hidden md:grid')}
      >
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
      </div>

      {params.toString() !== '' && (
        <Button
          variant="link"
          className="self-start px-0"
          onClick={() => {
            setTexto('')
            limpar()
          }}
        >
          Limpar filtros
        </Button>
      )}
    </div>
  )
}
