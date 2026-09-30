import type { SensorContext } from '@dnd-kit/core'
import { describe, expect, it } from 'vitest'
import { criarNavegacaoPorTeclado } from './navegacaoPorTeclado'

type Retangulo = { left: number; top: number; width: number; height: number }

/** Áreas do quadro com as posições dadas e o cartão parado sobre a área `sobre`. */
function contexto(areas: Record<number, Retangulo>, sobre: number | null) {
  return {
    collisionRect: { left: 10, top: 50, width: 100, height: 40, right: 110, bottom: 90 },
    droppableRects: new Map(Object.entries(areas).map(([id, r]) => [Number(id), r])),
    droppableContainers: { getEnabled: () => Object.keys(areas).map((id) => ({ id: Number(id) })) },
    over: sobre === null ? null : { id: sobre },
  } as unknown as SensorContext
}

const tecla = (code: string) => ({ code, preventDefault: () => {} }) as KeyboardEvent

const area = (left: number, top = 0): Retangulo => ({ left, top, width: 200, height: 600 })

/** O canto do cartão para ele ficar centralizado na área. */
const centralizadoEm = (r: Retangulo) => ({
  x: r.left + r.width / 2 - 50,
  y: r.top + r.height / 2 - 20,
})

describe('arrastar pelo teclado', () => {
  const areas = { 1: area(0), 2: area(220), 3: area(440), 4: area(660) }

  it('cada seta avança uma etapa, mesmo com a etapa embaixo do cartão ainda desatualizada', () => {
    const { pular, destino } = criarNavegacaoPorTeclado()
    expect(destino()).toBeNull()
    // O dnd-kit ainda diz que o cartão está sobre a primeira etapa em todas as setas, como quando elas
    // são apertadas depressa.
    const aindaNaPrimeira = contexto(areas, 1)
    const mover = (code: string) =>
      pular(tecla(code), {
        active: 1024,
        currentCoordinates: { x: 0, y: 0 },
        context: aindaNaPrimeira,
      })

    expect(mover('ArrowRight')).toEqual(centralizadoEm(areas[2]))
    expect(mover('ArrowRight')).toEqual(centralizadoEm(areas[3]))
    expect(mover('ArrowRight')).toEqual(centralizadoEm(areas[4]))
    expect(mover('ArrowRight')).toBeUndefined()
    expect(mover('ArrowLeft')).toEqual(centralizadoEm(areas[3]))
    expect(mover('ArrowDown')).toBeUndefined()
    // Ao soltar, vale a etapa escolhida pelas setas, e não a que o dnd-kit ainda informa.
    expect(destino()).toBe(3)
  })

  it('um arrasto novo recomeça da etapa do cartão', () => {
    const navegacao = criarNavegacaoPorTeclado()
    const argumentos = (sobre: number) => ({
      active: 1024,
      currentCoordinates: { x: 0, y: 0 },
      context: contexto(areas, sobre),
    })
    navegacao.pular(tecla('ArrowRight'), argumentos(1))
    navegacao.pular(tecla('ArrowRight'), argumentos(1))

    navegacao.reiniciar()

    expect(navegacao.destino()).toBeNull()
    expect(navegacao.pular(tecla('ArrowLeft'), argumentos(4))).toEqual(centralizadoEm(areas[3]))
  })

  it('segue a ordem de leitura quando as colunas passam para a linha de baixo', () => {
    const { pular } = criarNavegacaoPorTeclado()
    const emDuasLinhas = {
      5: area(220, 700),
      1: area(0),
      2: area(220),
      4: area(0, 700),
      3: area(440),
    }
    const mover = () =>
      pular(tecla('ArrowRight'), {
        active: 1024,
        currentCoordinates: { x: 0, y: 0 },
        context: contexto(emDuasLinhas, 1),
      })

    expect([mover(), mover(), mover(), mover()]).toEqual(
      [emDuasLinhas[2], emDuasLinhas[3], emDuasLinhas[4], emDuasLinhas[5]].map(centralizadoEm),
    )
  })

  it('fora de qualquer etapa, parte da mais próxima do cartão', () => {
    const { pular } = criarNavegacaoPorTeclado()

    const destino = pular(tecla('ArrowRight'), {
      active: 1024,
      currentCoordinates: { x: 0, y: 0 },
      context: contexto(areas, null),
    })

    expect(destino).toEqual(centralizadoEm(areas[2]))
  })
})
