import type { KeyboardCoordinateGetter, UniqueIdentifier } from '@dnd-kit/core'

const DIRECOES: Record<string, number> = { ArrowRight: 1, ArrowLeft: -1 }

type Retangulo = { left: number; top: number; width: number; height: number }

const centro = (r: Retangulo) => ({ x: r.left + r.width / 2, y: r.top + r.height / 2 })

/**
 * Arrastar pelo teclado: as setas para a direita e para a esquerda levam o cartão para a etapa seguinte ou
 * anterior, na ordem de leitura (da esquerda para a direita, de cima para baixo), em vez de andar alguns
 * pixels.
 *
 * A etapa escolhida fica guardada durante o arrasto, e é ela que vale ao soltar. A etapa embaixo do cartão
 * que o dnd-kit informa só se atualiza depois de a tela ser redesenhada: com as setas apertadas depressa,
 * cada seta partiria da mesma etapa, e soltar logo depois da última seta usaria a etapa anterior. E o quadro
 * não rola para o lado: numa área que rola, o dnd-kit troca o movimento por uma rolagem, e a contagem se
 * perde do mesmo jeito.
 */
export function criarNavegacaoPorTeclado() {
  let escolhida: { indice: number; id: UniqueIdentifier } | null = null

  const pular: KeyboardCoordinateGetter = (evento, { context }) => {
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
      .sort(
        (a, b) =>
          Math.round(a.retangulo.top) - Math.round(b.retangulo.top) ||
          a.retangulo.left - b.retangulo.left,
      )
    // Na primeira seta, parte da etapa embaixo do cartão ou, fora de todas, da mais próxima dele.
    const doCartao = centro(collisionRect)
    const distancia = (r: Retangulo) =>
      Math.hypot(centro(r).x - doCartao.x, centro(r).y - doCartao.y)
    const atual =
      escolhida?.indice ??
      (over
        ? areas.findIndex((area) => area.id === over.id)
        : areas.reduce(
            (maisPerto, area, indice) =>
              distancia(area.retangulo) < distancia(areas[maisPerto].retangulo)
                ? indice
                : maisPerto,
            0,
          ))
    const alvo = areas[atual + direcao]
    if (!alvo) {
      return undefined
    }
    escolhida = { indice: atual + direcao, id: alvo.id }
    return {
      x: centro(alvo.retangulo).x - collisionRect.width / 2,
      y: centro(alvo.retangulo).y - collisionRect.height / 2,
    }
  }

  return {
    pular,
    /** A etapa escolhida pelas setas neste arrasto; nula se o arrasto foi com o mouse ou sem setas. */
    destino: () => escolhida?.id ?? null,
    /** A cada arrasto novo, a contagem recomeça da etapa do cartão. */
    reiniciar: () => {
      escolhida = null
    },
  }
}
