import {
  closestCenter,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  pointerWithin,
  useDraggable,
  useDroppable,
  useSensor,
  useSensors,
  type Active,
  type Announcements,
  type CollisionDetection,
  type DragEndEvent,
  type DragStartEvent,
  type KeyboardCoordinateGetter,
  type Over,
} from '@dnd-kit/core'
import { GripVerticalIcon } from 'lucide-react'
import { useState, type KeyboardEventHandler } from 'react'
import { Link } from 'react-router'
import type {
  CartaoFunilResponse,
  ColunaFunilResponse,
  EtapaResumo,
  FiltrosFunil,
  QuadroFunilResponse,
} from '@/api/cliente'
import { cn } from '@/lib/utils'
import { CartaoLead } from './CartaoLead'
import { useMovimento } from './useMovimento'

type DadosDoCartao = { cartao: CartaoFunilResponse; etapaId: number }
type DadosDaArea = { etapa: EtapaResumo }

const cartaoDe = (ativo: Active) => (ativo.data.current as DadosDoCartao).cartao
const etapaDe = (area: Over) => (area.data.current as DadosDaArea).etapa

/** Com o mouse, vale a área embaixo do ponteiro; com o teclado, a área mais perto do cartão. */
const deteccao: CollisionDetection = (argumentos) => {
  const embaixoDoPonteiro = pointerWithin(argumentos)
  return embaixoDoPonteiro.length > 0 ? embaixoDoPonteiro : closestCenter(argumentos)
}

const DIRECOES: Record<string, number> = { ArrowRight: 1, ArrowDown: 1, ArrowLeft: -1, ArrowUp: -1 }

/**
 * Pelo teclado, cada seta leva o cartão para a etapa seguinte ou anterior (da esquerda para a direita, de
 * cima para baixo), em vez de andar alguns pixels.
 */
const pularEntreEtapas: KeyboardCoordinateGetter = (evento, { context }) => {
  const direcao = DIRECOES[evento.code]
  const { collisionRect, droppableRects, droppableContainers, over } = context
  if (!direcao || !collisionRect) {
    return undefined
  }
  evento.preventDefault()
  const areas = droppableContainers
    .getEnabled()
    .flatMap((area) => {
      const retangulo = droppableRects.get(area.id)
      return retangulo ? [{ id: area.id, retangulo }] : []
    })
    .sort((a, b) => a.retangulo.left - b.retangulo.left || a.retangulo.top - b.retangulo.top)
  const atual = areas.findIndex((area) => area.id === over?.id)
  const alvo = areas[atual + direcao]?.retangulo
  if (!alvo) {
    return undefined
  }
  return {
    x: alvo.left + alvo.width / 2 - collisionRect.width / 2,
    y: alvo.top + alvo.height / 2 - collisionRect.height / 2,
  }
}

const anuncios: Announcements = {
  onDragStart: ({ active }) => `Você pegou o lead ${cartaoDe(active).nome}.`,
  onDragOver: ({ active, over }) =>
    over
      ? `${cartaoDe(active).nome} está sobre ${etapaDe(over).nome}.`
      : `${cartaoDe(active).nome} não está sobre nenhuma etapa.`,
  onDragEnd: ({ active, over }) =>
    over
      ? `${cartaoDe(active).nome} foi solto em ${etapaDe(over).nome}.`
      : `${cartaoDe(active).nome} foi solto fora das etapas.`,
  onDragCancel: ({ active }) => `Você desistiu de mover ${cartaoDe(active).nome}.`,
}

const INSTRUCOES =
  'Para mover o lead, aperte espaço ou enter, use as setas para escolher a etapa e aperte espaço ou ' +
  'enter de novo para soltar. Esc cancela.'

type Props = {
  quadro: QuadroFunilResponse
  filtros: FiltrosFunil
  mostrarResponsavel: boolean
}

/**
 * Quadro do funil no computador (seção 7): uma coluna por etapa aberta, e Ganho e Perdido como áreas de
 * soltar. Arrasta com o mouse ou com o teclado, pela alça do cartão.
 */
export function QuadroFunil({ quadro, filtros, mostrarResponsavel }: Props) {
  const { soltar, pendenteId, dialogo } = useMovimento(filtros)
  const [arrastando, setArrastando] = useState<CartaoFunilResponse | null>(null)
  const sensores = useSensors(
    // Uma distância mínima, para o clique no nome ainda abrir a ficha.
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: pularEntreEtapas }),
  )

  const aoComecar = ({ active }: DragStartEvent) => setArrastando(cartaoDe(active))

  const aoSoltar = ({ active, over }: DragEndEvent) => {
    setArrastando(null)
    if (over) {
      const { cartao, etapaId } = active.data.current as DadosDoCartao
      soltar(cartao, etapaId, etapaDe(over))
    }
  }

  return (
    <section aria-label="Quadro do funil" className="hidden md:block">
      <DndContext
        sensors={sensores}
        collisionDetection={deteccao}
        accessibility={{
          announcements: anuncios,
          screenReaderInstructions: { draggable: INSTRUCOES },
        }}
        onDragStart={aoComecar}
        onDragEnd={aoSoltar}
        onDragCancel={() => setArrastando(null)}
      >
        <div className="flex items-start gap-3 overflow-x-auto pb-2">
          {quadro.colunas.map((coluna) => (
            <Coluna
              key={coluna.etapa.id}
              coluna={coluna}
              escondido={pendenteId}
              mostrarResponsavel={mostrarResponsavel}
            />
          ))}
          <div className="flex w-56 shrink-0 flex-col gap-3">
            <AreaDeFechamento area={quadro.ganho} />
            <AreaDeFechamento area={quadro.perdido} />
          </div>
        </div>
        {/* Sem a animação de volta: com o movimento otimista, o cartão já está na coluna nova ao soltar. */}
        <DragOverlay dropAnimation={null}>
          {arrastando && (
            <CartaoLead
              cartao={arrastando}
              mostrarResponsavel={mostrarResponsavel}
              className="w-64 shadow-lg"
            />
          )}
        </DragOverlay>
      </DndContext>
      {dialogo}
    </section>
  )
}

function Coluna({
  coluna,
  escondido,
  mostrarResponsavel,
}: {
  coluna: ColunaFunilResponse
  /** Cartão esperando o motivo da perda: sai da coluna até a pessoa confirmar ou cancelar. */
  escondido: number | null
  mostrarResponsavel: boolean
}) {
  const { etapa } = coluna
  const { setNodeRef, isOver } = useDroppable({ id: etapa.id, data: { etapa } })
  const cartoes = coluna.leads.filter((cartao) => cartao.id !== escondido)
  const titulo = `coluna-${etapa.id}`

  return (
    <div
      ref={setNodeRef}
      data-area-funil={etapa.id}
      role="group"
      aria-labelledby={titulo}
      className={cn(
        'flex w-64 shrink-0 flex-col gap-2 rounded-xl bg-muted/50 p-2 transition-colors',
        isOver && 'bg-accent ring-2 ring-ring/40',
      )}
    >
      <h2 className="flex items-center justify-between px-1 text-sm font-medium">
        <span id={titulo}>{etapa.nome}</span>
        <span className="text-xs font-normal text-muted-foreground">{coluna.total}</span>
      </h2>
      <ul className="flex min-h-16 flex-col gap-2">
        {cartoes.map((cartao) => (
          <CartaoArrastavel
            key={cartao.id}
            cartao={cartao}
            etapaId={etapa.id}
            mostrarResponsavel={mostrarResponsavel}
          />
        ))}
      </ul>
      {coluna.total > coluna.leads.length && (
        <Link
          to={`/leads?etapa=${etapa.id}`}
          className="px-1 text-xs text-muted-foreground hover:underline"
        >
          Ver todos os {coluna.total} na lista
        </Link>
      )}
    </div>
  )
}

function CartaoArrastavel({
  cartao,
  etapaId,
  mostrarResponsavel,
}: {
  cartao: CartaoFunilResponse
  etapaId: number
  mostrarResponsavel: boolean
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, isDragging } = useDraggable({
    id: cartao.id,
    data: { cartao, etapaId } satisfies DadosDoCartao,
    attributes: { roleDescription: 'cartão arrastável' },
  })
  // O mouse arrasta pelo cartão inteiro; o teclado, pela alça, para o Enter no nome ainda abrir a ficha.
  const { onKeyDown, ...doPonteiro } = listeners ?? {}
  const aoTeclar = onKeyDown as KeyboardEventHandler<HTMLButtonElement> | undefined

  return (
    <li ref={setNodeRef} {...doPonteiro} className={cn('touch-none', isDragging && 'opacity-40')}>
      <CartaoLead
        cartao={cartao}
        mostrarResponsavel={mostrarResponsavel}
        alca={
          <button
            ref={setActivatorNodeRef}
            type="button"
            {...attributes}
            onKeyDown={aoTeclar}
            aria-label={`Mover ${cartao.nome}`}
            className="-m-1 cursor-grab rounded-md p-1 text-muted-foreground outline-none hover:bg-accent focus-visible:ring-3 focus-visible:ring-ring/50"
          >
            <GripVerticalIcon className="size-4" aria-hidden />
          </button>
        }
      />
    </li>
  )
}

function AreaDeFechamento({ area }: { area: QuadroFunilResponse['ganho'] }) {
  const { etapa, periodo } = area
  const { setNodeRef, isOver } = useDroppable({ id: etapa.id, data: { etapa } })
  const titulo = `area-${etapa.id}`

  return (
    <div
      ref={setNodeRef}
      data-area-funil={etapa.id}
      role="group"
      aria-labelledby={titulo}
      className={cn(
        'flex min-h-28 flex-col justify-between gap-1 rounded-xl border-2 border-dashed p-3 text-sm transition-colors',
        etapa.tipo === 'ganho' ? 'border-primary/40' : 'border-destructive/40',
        isOver && (etapa.tipo === 'ganho' ? 'bg-primary/10' : 'bg-destructive/10'),
      )}
    >
      <h2 id={titulo} className="font-medium">
        {etapa.nome}
      </h2>
      <p className="text-muted-foreground">
        {periodo === 1 ? '1 lead' : `${periodo} leads`} nos últimos 30 dias
      </p>
      <Link
        to={`/leads?etapa=${etapa.id}`}
        className="text-xs text-muted-foreground hover:underline"
      >
        Ver na lista
      </Link>
    </div>
  )
}
