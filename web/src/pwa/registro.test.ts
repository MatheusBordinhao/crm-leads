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

  it('uma falha no registro não quebra a tela', async () => {
    comNavegador({
      register: vi.fn(async () => {
        throw new DOMException('Service workers are disabled', 'SecurityError')
      }),
    })

    await expect(registrarServiceWorker(true)).resolves.toBeUndefined()
  })

  it('no https://localhost do Compose de produção local, nem tenta: o certificado do Caddy não é confiável', async () => {
    const register = vi.fn(async () => ({}))
    comNavegador({ register })

    await registrarServiceWorker(true, { protocol: 'https:', hostname: 'localhost' })
    expect(register).not.toHaveBeenCalled()

    // No servidor, com o certificado de verdade, e no npm run preview (http://localhost:4173), registra.
    await registrarServiceWorker(true, { protocol: 'https:', hostname: 'crm.empresa.com.br' })
    await registrarServiceWorker(true, { protocol: 'http:', hostname: 'localhost' })
    expect(register).toHaveBeenCalledTimes(2)
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
