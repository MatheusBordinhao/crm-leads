import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { toast } from 'sonner'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { servidor } from './servidor'

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
