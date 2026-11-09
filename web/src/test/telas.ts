import type { RouteObject } from 'react-router'

/** Cada rota da árvore, com as filhas logo depois da mãe. */
export function todasAsRotas(lista: RouteObject[]): RouteObject[] {
  return lista.flatMap((rota) => [rota, ...todasAsRotas(rota.children ?? [])])
}

/** Chama o `lazy` de cada rota sob demanda, as mesmas funções que o roteador chama, e devolve o que elas carregaram. */
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

/**
 * A mesma árvore, com cada tela sob demanda já carregada: o `Component` que o `lazy` devolve fica no lugar dele, e a
 * tela aparece sem a espera do `lazy`, como quando o rotas.tsx importava as telas direto.
 */
export async function comAsTelasCarregadas(lista: RouteObject[]): Promise<RouteObject[]> {
  return Promise.all(
    lista.map(async ({ lazy, children, ...rota }) => {
      const carregada = typeof lazy === 'function' ? await lazy() : {}
      const filhas = children ? { children: await comAsTelasCarregadas(children) } : {}
      return { ...rota, ...carregada, ...filhas } as RouteObject
    }),
  )
}
