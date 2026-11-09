import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  CHAVE_RECARGA,
  INTERVALO_ENTRE_RECARGAS,
  marcarRecarga,
  podeRecarregarSozinha,
} from './recarga'

const agora = 1_800_000_000_000

describe('a trava da recarga automática', () => {
  afterEach(() => {
    sessionStorage.clear()
    vi.restoreAllMocks()
  })

  it('sem marca, a página pode recarregar sozinha', () => {
    expect(podeRecarregarSozinha(agora)).toBe(true)
  })

  it('com uma recarga há 59 s, não pode; há 60 s, pode', () => {
    sessionStorage.setItem(CHAVE_RECARGA, String(agora - 59_000))
    expect(podeRecarregarSozinha(agora)).toBe(false)

    sessionStorage.setItem(CHAVE_RECARGA, String(agora - INTERVALO_ENTRE_RECARGAS))
    expect(podeRecarregarSozinha(agora)).toBe(true)
  })

  it('a marca grava a hora, e a recarga seguinte espera um minuto', () => {
    expect(marcarRecarga(agora)).toBe(true)

    expect(sessionStorage.getItem(CHAVE_RECARGA)).toBe(String(agora))
    expect(podeRecarregarSozinha(agora + 1_000)).toBe(false)
  })

  it('com o sessionStorage que falha, não recarrega sozinha, para nunca entrar em laço', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('armazenamento bloqueado')
    })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('armazenamento bloqueado')
    })

    expect(podeRecarregarSozinha(agora)).toBe(false)
    expect(marcarRecarga(agora)).toBe(false)
  })
})
