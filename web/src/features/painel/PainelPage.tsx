import { ListFilterIcon } from 'lucide-react'
import { useId, useState, type ReactNode } from 'react'
import { useSearchParams } from 'react-router'
import { ErroApi, type FiltrosPainel, type Origem, type PainelResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Selecao } from '@/components/Selecao'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useEu } from '@/features/acesso/sessao'
import { useCampanhas } from '@/features/leads/dados'
import { ORIGENS, ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { useUsuarios } from '@/features/usuarios/dados'
import { formatarDia, formatarDuracao } from '@/lib/datas'
import { mensagemDoErro } from '@/lib/formularios'
import { formatarNumero, formatarTaxa } from '@/lib/numeros'
import { cn } from '@/lib/utils'
import { Barras } from './Barras'
import { usePainel } from './dados'
import { Indicador } from './Indicador'
import {
  hojeEmBrasilia,
  intervaloDoAtalho,
  intervaloDoEndereco,
  lerPeriodo,
  MAIOR_DIA,
  MENOR_DIA,
  PERIODOS,
  ROTULOS_PERIODO,
  type Periodo,
} from './periodo'
import { TabelaAtendentes } from './TabelaAtendentes'

/** Valor do filtro de responsável para os leads que ainda não têm, como no funil e na lista. */
const SEM_RESPONSAVEL = 'sem'

/**
 * A linha dos leads sem campanha, que a API manda sempre por último. Eles não vieram de anúncio (cadastro manual, site,
 * indicação, planilha), então o rótulo diz isso, para a linha não parecer uma campanha.
 */
const SEM_CAMPANHA = 'Sem campanha (não veio de anúncio)'

/**
 * Filtros guardados no endereço, com os nomes do funil e da lista: `periodo` para os atalhos, `de` e `ate` para o
 * personalizado, `origem`, `campanha` e `responsavel`.
 */
function lerFiltros(params: URLSearchParams, hoje: string): FiltrosPainel {
  const origem = params.get('origem')
  const campanha = params.get('campanha')
  const responsavel = params.get('responsavel')
  return {
    ...intervaloDoEndereco(params, hoje),
    origem: ORIGENS.includes(origem as Origem) ? (origem as Origem) : undefined,
    campanhaId: campanha && /^\d+$/.test(campanha) ? Number(campanha) : undefined,
    responsavelId: responsavel && responsavel !== SEM_RESPONSAVEL ? responsavel : undefined,
    semResponsavel: responsavel === SEM_RESPONSAVEL || undefined,
  }
}

type Mudar = (mudancas: Record<string, string | null>) => void

/** O painel (seção 8): a tela inicial do admin. A atendente o abre pelo menu e vê só os próprios números. */
export function PainelPage() {
  const { data: eu } = useEu()
  const ehAdmin = eu?.perfil === 'Admin'
  const [params, setParams] = useSearchParams()
  const hoje = hojeEmBrasilia()
  const {
    data: painel,
    isPending,
    isError,
    error,
    refetch,
    isPlaceholderData,
  } = usePainel(lerFiltros(params, hoje))

  const mudar: Mudar = (mudancas) => {
    const novos = new URLSearchParams(params)
    for (const [nome, valor] of Object.entries(mudancas)) {
      if (valor) {
        novos.set(nome, valor)
      } else {
        novos.delete(nome)
      }
    }
    setParams(novos, { replace: true })
  }

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold">Painel</h1>
        {painel && (
          <p className="text-sm text-muted-foreground">
            {formatarDia(painel.de)} a {formatarDia(painel.ate)}
          </p>
        )}
      </div>

      <Filtros
        params={params}
        mudar={mudar}
        limpar={() => setParams(new URLSearchParams(), { replace: true })}
        ehAdmin={ehAdmin}
        intervaloNaTela={
          painel ? { de: painel.de, ate: painel.ate } : intervaloDoAtalho('30d', hoje)
        }
      />

      {isPending && <Carregando />}
      {isError && <Falha erro={error} tentarDeNovo={() => refetch()} />}
      {painel && <Numeros painel={painel} ehAdmin={ehAdmin} atualizando={isPlaceholderData} />}
    </section>
  )
}

/** Um período inválido mostra a mensagem da API; as outras falhas oferecem tentar de novo. */
function Falha({ erro, tentarDeNovo }: { erro: Error; tentarDeNovo: () => void }) {
  if (erro instanceof ErroApi && erro.status === 400) {
    return (
      <p role="alert" className="text-sm text-destructive">
        {mensagemDoErro(erro)}
      </p>
    )
  }
  return (
    <div className="flex flex-col items-start gap-2">
      <p>Não foi possível carregar o painel.</p>
      <Button variant="outline" onClick={tentarDeNovo}>
        Tentar de novo
      </Button>
    </div>
  )
}

function Filtros({
  params,
  mudar,
  limpar,
  ehAdmin,
  intervaloNaTela,
}: {
  params: URLSearchParams
  mudar: Mudar
  limpar: () => void
  ehAdmin: boolean
  intervaloNaTela: { de: string; ate: string }
}) {
  const { data: campanhas } = useCampanhas()
  const { data: usuarios } = useUsuarios({ enabled: ehAdmin })
  const [abertos, setAbertos] = useState(false)
  const periodo = lerPeriodo(params)

  // O personalizado começa com o período que está na tela.
  const mudarPeriodo = (novo: Periodo) =>
    novo === 'personalizado'
      ? mudar({ periodo: null, ...intervaloNaTela })
      : mudar({ periodo: novo === '30d' ? null : novo, de: null, ate: null })

  const campo = (nome: string, rotulo: string, controle: ReactNode) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`painel-${nome}`}>{rotulo}</Label>
      {controle}
    </div>
  )

  const selecao = (nome: string, rotulo: string, opcoes: ReactNode) =>
    campo(
      nome,
      rotulo,
      <Selecao
        id={`painel-${nome}`}
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar({ [nome]: evento.target.value })}
      >
        {opcoes}
      </Selecao>,
    )

  const data = (nome: 'de' | 'ate', rotulo: string) =>
    campo(
      nome,
      rotulo,
      <Input
        id={`painel-${nome}`}
        type="date"
        min={MENOR_DIA}
        max={MAIOR_DIA}
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar({ [nome]: evento.target.value })}
      />,
    )

  return (
    <div className="flex flex-col gap-3">
      {/* Uma linha só no computador; no celular, o período fica sempre à vista e o resto, atrás de Filtros. */}
      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        <div className="flex min-w-0 items-end gap-2">
          <div className="flex min-w-0 flex-1 flex-col gap-1.5">
            <Label htmlFor="painel-periodo">Período</Label>
            <Selecao
              id="painel-periodo"
              value={periodo}
              onChange={(evento) => mudarPeriodo(evento.target.value as Periodo)}
            >
              {PERIODOS.map((opcao) => (
                <option key={opcao} value={opcao}>
                  {ROTULOS_PERIODO[opcao]}
                </option>
              ))}
            </Selecao>
          </div>
          <Button
            type="button"
            variant="outline"
            className="md:hidden"
            aria-expanded={abertos}
            aria-controls="filtros-painel"
            onClick={() => setAbertos(!abertos)}
          >
            <ListFilterIcon aria-hidden />
            Filtros
          </Button>
        </div>

        {periodo === 'personalizado' && (
          <div className="grid grid-cols-2 gap-3 md:contents">
            {data('de', 'De')}
            {data('ate', 'Até')}
          </div>
        )}

        <div
          id="filtros-painel"
          className={cn('grid gap-3 sm:grid-cols-2 md:contents', !abertos && 'hidden md:contents')}
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
      </div>

      {params.toString() !== '' && (
        <Button variant="link" className="self-start px-0" onClick={limpar}>
          Limpar filtros
        </Button>
      )}
    </div>
  )
}

/** Um cartão do painel, com o título que dá nome à lista ou à tabela dentro dele. */
function Secao({
  titulo,
  descricao,
  children,
}: {
  titulo: string
  descricao: string
  children: (tituloId: string) => ReactNode
}) {
  const id = useId()
  return (
    <Card>
      <CardHeader>
        <h2 id={id} className="font-heading text-base leading-snug font-medium">
          {titulo}
        </h2>
        <CardDescription>{descricao}</CardDescription>
      </CardHeader>
      <CardContent>{children(id)}</CardContent>
    </Card>
  )
}

function Vazio({ texto = 'Nenhum lead novo no período.' }: { texto?: string }) {
  return <p className="text-sm text-muted-foreground">{texto}</p>
}

/** "1 ganho · 25%", para as linhas por origem e por campanha. */
function ganhosEConversao({
  ganhos,
  taxaConversao,
}: {
  ganhos: number
  taxaConversao: number | null
}) {
  const texto = ganhos === 1 ? '1 ganho' : `${formatarNumero(ganhos)} ganhos`
  return `${texto} · ${formatarTaxa(taxaConversao)}`
}

function Numeros({
  painel,
  ehAdmin,
  atualizando,
}: {
  painel: PainelResponse
  ehAdmin: boolean
  atualizando: boolean
}) {
  const { primeiroContato, perdas, porAtendente } = painel
  const semContato = primeiroContato.semContato
  const perdidos =
    perdas.leads === 1
      ? '1 lead foi para Perdido no período'
      : `${formatarNumero(perdas.leads)} leads foram para Perdido no período`

  return (
    // Com um filtro novo carregando, os números anteriores ficam na tela, mais claros.
    <div
      aria-busy={atualizando}
      className={cn('flex flex-col gap-4 transition-opacity', atualizando && 'opacity-60')}
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Indicador
          rotulo="Leads novos"
          valor={formatarNumero(painel.leadsNovos)}
          detalhe="criados no período"
        />
        <Indicador
          rotulo="Entradas"
          valor={formatarNumero(painel.entradas)}
          detalhe="com as de quem já era lead"
        />
        <Indicador
          rotulo="Conversão"
          valor={formatarTaxa(painel.taxaConversao)}
          detalhe={
            painel.leadsNovos === 0
              ? 'sem leads novos'
              : `${formatarNumero(painel.ganhos)} de ${formatarNumero(painel.leadsNovos)} em Ganho`
          }
        />
        <Indicador
          rotulo="Primeiro contato"
          valor={
            primeiroContato.medianaMinutos === null
              ? '—'
              : formatarDuracao(primeiroContato.medianaMinutos)
          }
          detalhe={
            semContato === 0 ? 'mediana' : `mediana · ${formatarNumero(semContato)} sem contato`
          }
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Secao
          titulo="Situação atual"
          descricao="Leads novos do período, pela etapa em que estão hoje."
        >
          {(id) =>
            painel.leadsNovos === 0 ? (
              <Vazio />
            ) : (
              <Barras
                rotuloId={id}
                itens={painel.situacaoAtual.map(({ etapa, leadsNovos }) => ({
                  chave: etapa.id,
                  rotulo: etapa.nome,
                  valor: leadsNovos,
                }))}
              />
            )
          }
        </Secao>

        <Secao
          titulo="Motivos de perda"
          descricao={
            perdas.leads === 0
              ? 'Leads que foram para Perdido no período, de qualquer data de entrada.'
              : `${perdidos}, de qualquer data de entrada.`
          }
        >
          {(id) =>
            perdas.porMotivo.length === 0 ? (
              <Vazio texto="Nenhum lead foi para Perdido no período." />
            ) : (
              <Barras
                rotuloId={id}
                itens={perdas.porMotivo.map(({ motivo, leads }) => ({
                  chave: motivo?.id ?? 'sem-motivo',
                  rotulo: motivo?.nome ?? 'Sem motivo',
                  valor: leads,
                }))}
              />
            )
          }
        </Secao>

        <Secao
          titulo="Por origem"
          descricao="Leads novos, pela origem da primeira entrada, e quantos viraram clientes."
        >
          {(id) =>
            painel.porOrigem.length === 0 ? (
              <Vazio />
            ) : (
              <Barras
                rotuloId={id}
                itens={painel.porOrigem.map((linha) => ({
                  chave: linha.origem,
                  rotulo: ROTULOS_ORIGEM[linha.origem],
                  valor: linha.leadsNovos,
                  detalhe: ganhosEConversao(linha),
                }))}
              />
            )
          }
        </Secao>

        <Secao
          titulo="Por campanha"
          descricao="Leads novos, pela campanha da primeira entrada, e quantos viraram clientes."
        >
          {(id) =>
            painel.porCampanha.length === 0 ? (
              <Vazio />
            ) : (
              <Barras
                rotuloId={id}
                itens={painel.porCampanha.map((linha) => ({
                  chave: linha.campanha?.id ?? 'sem-campanha',
                  rotulo: linha.campanha?.nome ?? SEM_CAMPANHA,
                  valor: linha.leadsNovos,
                  detalhe: ganhosEConversao(linha),
                }))}
              />
            )
          }
        </Secao>
      </div>

      {ehAdmin && porAtendente && (
        <Secao
          titulo="Por atendente"
          descricao="Leads novos do período, pela responsável atual. O primeiro contato é a mediana."
        >
          {(id) => <TabelaAtendentes atendentes={porAtendente} tituloId={id} />}
        </Secao>
      )}
    </div>
  )
}
