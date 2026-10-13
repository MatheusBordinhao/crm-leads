import { afterEach, describe, expect, it, vi } from 'vitest'
import { comServiceWorker, registrarServiceWorker } from './registro'

/** O jsdom não tem service worker: o teste põe um falso no navigator. */
function comNavegador(serviceWorker: object) {
  Object.defineProperty(navigator, 'serviceWorker', { configurable: true, value: serviceWorker })
}

afterEach(() => {
  Reflect.deleteProperty(navigator, 'serviceWorker')
})

describe('registro do service worker', () => {
  it('em desenvolvimento, não registra nada', async () => {
    const register = vi.fn(async () => ({}))
    comNavegador({ register })

    await registrarServiceWorker(false)

    expect(register).not.toHaveBeenCalled()
  })

  it('no build de produção, registra o /sw.js para o site inteiro, sem o cache do HTTP', async () => {
    const register = vi.fn(async () => ({}))
    comNavegador({ register })

    await registrarServiceWorker(true)

    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/', updateViaCache: 'none' })
  })

  it('uma falha no registro, como o certificado local do Caddy, não quebra a tela', async () => {
    comNavegador({
      register: vi.fn(async () => {
        throw new DOMException('An SSL certificate error occurred', 'SecurityError')
      }),
    })

    await expect(registrarServiceWorker(true)).resolves.toBeUndefined()
  })

  it('num navegador sem service worker, não faz nada', async () => {
    await expect(registrarServiceWorker(true)).resolves.toBeUndefined()
    expect(comServiceWorker()).toBe(false)
  })

  it('a notificação sai pelo service worker só com a página controlada por ele', () => {
    comNavegador({ controller: null })
    expect(comServiceWorker()).toBe(false)

    comNavegador({ controller: {} })
    expect(comServiceWorker()).toBe(true)
  })
})
