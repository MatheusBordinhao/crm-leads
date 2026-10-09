import { useMemo } from 'react'
import { Link, useSearchParams } from 'react-router'
import { ErroApi, type FiltrosAuditoria, type RegistroAuditoriaResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Paginacao } from '@/components/Paginacao'
import { Selecao } from '@/components/Selecao'
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
import { useEtapas, useMotivosPerda } from '@/features/leads/dados'
import { useUsuarios } from '@/features/usuarios/dados'
import { formatarDataHora } from '@/lib/datas'
import { mensagemDoErro } from '@/lib/formularios'
import { cn } from '@/lib/utils'
import { useAuditoria } from './dados'
import {
  descreverDetalhes,
  descreverEntidade,
  GRUPOS_DE_ACOES,
  rotuloAcao,
  type Nomes,
} from './rotulos'

type Mudar = (nome: string, valor: string) => void

/**
 * Filtros guardados no endereço (`/auditoria?acao=lead.anonimizado&usuario=…`). A ficha abre a tela com `lead`, que
 * vira o filtro pelo registro afetado.
 */
function lerFiltros(params: URLSearchParams): FiltrosAuditoria {
  const lead = params.get('lead')
  const pagina = params.get('pagina')
  const doLead = lead !== null && /^\d+$/.test(lead)
  return {
    de: params.get('de') || undefined,
    ate: params.get('ate') || undefined,
    usuarioId: params.get('usuario') || undefined,
    acao: params.get('acao') || undefined,
    entidade: doLead ? 'lead' : undefined,
    entidadeId: doLead ? lead : undefined,
    pagina: pagina && /^\d+$/.test(pagina) ? Number(pagina) : undefined,
  }
}

/** Os nomes dos usuários, das etapas e dos motivos de perda, para mostrar quem é cada id dos registros. */
function useNomes(): Nomes {
  const { data: usuarios } = useUsuarios()
  const { data: etapas } = useEtapas()
  const { data: motivos } = useMotivosPerda()
  return useMemo(() => {
    const porUsuario = new Map(usuarios?.map((usuario) => [usuario.id, usuario.nome]))
    const porEtapa = new Map(etapas?.map((etapa) => [etapa.id, etapa.nome]))
    const porMotivo = new Map(motivos?.map((motivo) => [motivo.id, motivo.nome]))
    return {
      usuario: (id) => porUsuario.get(id),
      etapa: (id) => porEtapa.get(id),
      motivo: (id) => porMotivo.get(id),
    }
  }, [usuarios, etapas, motivos])
}

/**
 * Auditoria (seção 3.8), só para o admin: quem fez o quê e quando, do mais recente para o mais antigo. Os registros não
 * têm dados pessoais: os leads aparecem pelo número.
 */
export function AuditoriaPage() {
  const [params, setParams] = useSearchParams()
  const filtros = lerFiltros(params)
  const { data, isPending, isError, error, refetch, isPlaceholderData } = useAuditoria(filtros)
  const nomes = useNomes()

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

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold">Auditoria</h1>
        <p className="text-sm text-muted-foreground">
          Quem fez o quê e quando: os logins, as mudanças nos leads, nos usuários e nas
          configurações, as exportações e a LGPD. Os registros não têm dados pessoais: os leads
          aparecem pelo número.
        </p>
      </div>

      <Filtros params={params} mudar={mudar} />

      {filtros.entidadeId && (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          <span>
            Só os registros do{' '}
            <Link to={`/leads/${filtros.entidadeId}`} className="font-medium hover:underline">
              lead {filtros.entidadeId}
            </Link>
          </span>
          <Button variant="link" size="sm" className="px-0" onClick={() => mudar('lead', '')}>
            Ver todos os registros
          </Button>
        </div>
      )}

      {isPending && <Carregando />}
      {isError && <Falha erro={error} tentarDeNovo={() => refetch()} />}
      {data && !isError && (
        // Com outros filtros carregando, os registros anteriores ficam na tela, mais claros.
        <div
          aria-busy={isPlaceholderData}
          className={cn(
            'flex flex-col gap-4 transition-opacity',
            isPlaceholderData && 'opacity-60',
          )}
        >
          <p className="text-sm text-muted-foreground" aria-live="polite">
            {data.total === 1 ? '1 registro' : `${data.total} registros`}
          </p>
          {data.itens.length === 0 ? (
            <p className="py-8 text-center text-muted-foreground">Nenhum registro encontrado.</p>
          ) : (
            <>
              <TabelaAuditoria registros={data.itens} nomes={nomes} />
              <CartoesAuditoria registros={data.itens} nomes={nomes} />
            </>
          )}
          <Paginacao
            pagina={data.pagina}
            total={data.total}
            tamanho={data.tamanho}
            irPara={(pagina) => mudar('pagina', String(pagina))}
          />
        </div>
      )}
    </section>
  )
}

function Filtros({ params, mudar }: { params: URLSearchParams; mudar: Mudar }) {
  const { data: usuarios } = useUsuarios()

  const data = (nome: string, rotulo: string) => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <Label htmlFor={`auditoria-${nome}`}>{rotulo}</Label>
      <Input
        id={`auditoria-${nome}`}
        type="date"
        value={params.get(nome) ?? ''}
        onChange={(evento) => mudar(nome, evento.target.value)}
      />
    </div>
  )

  return (
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <div className="flex min-w-0 flex-col gap-1.5">
        <Label htmlFor="auditoria-acao">Ação</Label>
        <Selecao
          id="auditoria-acao"
          value={params.get('acao') ?? ''}
          onChange={(evento) => mudar('acao', evento.target.value)}
        >
          <option value="">Todas</option>
          {GRUPOS_DE_ACOES.map((grupo) => (
            <optgroup key={grupo.titulo} label={grupo.titulo}>
              {grupo.acoes.map(([codigo, rotulo]) => (
                <option key={codigo} value={codigo}>
                  {rotulo}
                </option>
              ))}
            </optgroup>
          ))}
        </Selecao>
      </div>
      <div className="flex min-w-0 flex-col gap-1.5">
        <Label htmlFor="auditoria-usuario">Quem fez</Label>
        <Selecao
          id="auditoria-usuario"
          value={params.get('usuario') ?? ''}
          onChange={(evento) => mudar('usuario', evento.target.value)}
        >
          <option value="">Todos</option>
          {usuarios?.map((usuario) => (
            <option key={usuario.id} value={usuario.id}>
              {usuario.ativo ? usuario.nome : `${usuario.nome} (desativado)`}
            </option>
          ))}
        </Selecao>
      </div>
      {data('de', 'A partir de')}
      {data('ate', 'Até')}
    </div>
  )
}

/** O registro afetado: os leads e as importações abrem a tela deles. */
function Sobre({ registro, nomes }: { registro: RegistroAuditoriaResponse; nomes: Nomes }) {
  const { entidade, entidadeId } = registro
  if (!entidade) {
    return <span className="text-muted-foreground">—</span>
  }
  const descricao = descreverEntidade(entidade, entidadeId, nomes)
  const destino =
    entidadeId && entidade === 'lead'
      ? `/leads/${entidadeId}`
      : entidadeId && entidade === 'importacao'
        ? `/importacao/${entidadeId}`
        : null
  return destino ? (
    <Link to={destino} className="hover:underline">
      {descricao}
    </Link>
  ) : (
    <span>{descricao}</span>
  )
}

type PropsLista = { registros: RegistroAuditoriaResponse[]; nomes: Nomes }

function TabelaAuditoria({ registros, nomes }: PropsLista) {
  return (
    <div className="hidden rounded-lg border md:block">
      <Table aria-label="Registros da auditoria">
        <TableHeader>
          <TableRow>
            <TableHead>Quando</TableHead>
            <TableHead>Quem</TableHead>
            <TableHead>Ação</TableHead>
            <TableHead>Sobre</TableHead>
            <TableHead>Detalhes</TableHead>
            <TableHead>IP</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {registros.map((registro) => (
            <TableRow key={registro.id}>
              <TableCell>{formatarDataHora(registro.ocorridoEm)}</TableCell>
              <TableCell className="whitespace-normal">
                {registro.usuario?.nome ?? (
                  <span className="text-muted-foreground">Sem usuário</span>
                )}
              </TableCell>
              <TableCell className="whitespace-normal">{rotuloAcao(registro.acao)}</TableCell>
              <TableCell className="whitespace-normal">
                <Sobre registro={registro} nomes={nomes} />
              </TableCell>
              <TableCell className="min-w-48 whitespace-normal break-words text-muted-foreground">
                {descreverDetalhes(registro.acao, registro.detalhes, nomes) ?? '—'}
              </TableCell>
              <TableCell className="text-muted-foreground">{registro.ip ?? '—'}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

function CartoesAuditoria({ registros, nomes }: PropsLista) {
  return (
    <ul aria-label="Registros da auditoria" className="flex flex-col gap-3 md:hidden">
      {registros.map((registro) => {
        const detalhes = descreverDetalhes(registro.acao, registro.detalhes, nomes)
        return (
          <li key={registro.id}>
            <Card>
              <CardContent className="flex flex-col gap-1 text-sm">
                <div className="flex items-start justify-between gap-2">
                  <span className="font-medium">{rotuloAcao(registro.acao)}</span>
                  <time
                    dateTime={registro.ocorridoEm}
                    className="shrink-0 text-xs text-muted-foreground"
                  >
                    {formatarDataHora(registro.ocorridoEm)}
                  </time>
                </div>
                {registro.entidade && (
                  <div className="min-w-0 break-words">
                    <Sobre registro={registro} nomes={nomes} />
                  </div>
                )}
                {detalhes && <div className="break-words text-muted-foreground">{detalhes}</div>}
                <div className="flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted-foreground">
                  <span>{registro.usuario?.nome ?? 'Sem usuário'}</span>
                  {registro.ip && <span>IP {registro.ip}</span>}
                </div>
              </CardContent>
            </Card>
          </li>
        )
      })}
    </ul>
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
      <p>Não foi possível carregar a auditoria.</p>
      <Button variant="outline" onClick={tentarDeNovo}>
        Tentar de novo
      </Button>
    </div>
  )
}
