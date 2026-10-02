import { describe, expect, it } from 'vitest'
import { z } from './zod'

/** Roda `executar` com o `Function` global trocado por um que anota cada chamada e cada `new`. */
function anotarFunction<T>(executar: () => T) {
  const original = globalThis.Function
  const usos: unknown[][] = []
  globalThis.Function = new Proxy(original, {
    apply(alvo, este, argumentos) {
      usos.push(argumentos)
      return Reflect.apply(alvo, este, argumentos)
    },
    construct(alvo, argumentos, novoAlvo) {
      usos.push(argumentos)
      return Reflect.construct(alvo, argumentos, novoAlvo)
    },
  })
  try {
    return { resultado: executar(), usos }
  } finally {
    globalThis.Function = original
  }
}

describe('Zod', () => {
  it('cria e confere esquemas sem new Function, que a CSP de produção bloqueia', () => {
    const { resultado, usos } = anotarFunction(() => {
      const esquema = z.object({ nome: z.string().trim().min(1), idade: z.number().int() })
      return [
        esquema.safeParse({ nome: 'Maria', idade: 30 }).success,
        esquema.safeParse({ nome: ' ', idade: 1.5 }).success,
      ]
    })

    expect(usos).toEqual([])
    expect(resultado).toEqual([true, false])
  })
})
