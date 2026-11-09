import type { RouteObject } from 'react-router'

/** Cada rota da árvore, com as filhas logo depois da mãe. */
export function todasAsRotas(lista: RouteObject[]): RouteObject[] {
  return lista.flatMap((rota) => [rota, ...todasAsRotas(rota.children ?? [])])
}

/**
 * Chama o `lazy` de cada rota sob demanda, as mesmas funções que o roteador chama, e devolve o que elas carregaram. Com
 * o import em cache, a tela não paga o carregamento dentro da espera de um `findBy`.
 */
export async function carregarTelas(lista: RouteObject[]) {
  return Promise.all(
    todasAsRotas(lista)
      .filter((rota) => typeof rota.lazy === 'function')
      .map(async (rota) => ({
        rota,
        carregada: await (rota.lazy as () => Promise<RouteObject>)(),
      })),
  )
}
