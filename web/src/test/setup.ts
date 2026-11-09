import '@testing-library/jest-dom/vitest'
import { cleanup, configure, getConfig } from '@testing-library/react'
import { toast } from 'sonner'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { servidor } from './servidor'

// Numa espera (o waitFor e os findBy), cada tentativa que falha monta o erro do getBy, e o getElementError padrão
// formata a página inteira (prettyDOM) para um erro que o waitFor descarta. Nas tentativas, a Testing Library liga a
// marca _disableExpensiveErrorDiagnostics, e o erro sai só com a mensagem. Fora delas, e no erro final do tempo
// esgotado, que passa de novo pelo getElementError, a página continua no erro (esperas.test.tsx).
const erroComAPagina = getConfig().getElementError
configure({
  getElementError(mensagem, container) {
    const { _disableExpensiveErrorDiagnostics: emUmaTentativa } = getConfig() as {
      _disableExpensiveErrorDiagnostics?: boolean
    }
    if (emUmaTentativa) {
      const erro = new Error(mensagem ?? '')
      erro.name = 'TestingLibraryElementError'
      return erro
    }
    return erroComAPagina(mensagem, container)
  },
})

// O jsdom não implementa estas APIs, que os componentes do Radix e os avisos do sonner usam.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
}
Element.prototype.hasPointerCapture ??= () => false
Element.prototype.setPointerCapture ??= () => {}
Element.prototype.releasePointerCapture ??= () => {}
Element.prototype.scrollIntoView ??= () => {}
window.matchMedia ??= (query: string) =>
  ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }) as MediaQueryList

beforeAll(() => servidor.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  cleanup()
  servidor.resetHandlers()
  // Os toasts ficam num estado global do sonner: sem isso, o de um teste aparece de novo no seguinte.
  toast.dismiss()
})

afterAll(() => servidor.close())
