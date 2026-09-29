import { describe, expect, it } from 'vitest'
import { documentoValido, normalizarDocumento } from './documento'

// Os valores de teste da seção 3.1 da especificação.
const VALIDOS = ['12ABC34501DE35', '11222333000181', '52998224725']

describe('CPF e CNPJ', () => {
  it.each(VALIDOS)('%s é válido', (documento) => {
    expect(documentoValido(normalizarDocumento(documento))).toBe(true)
  })

  it.each(VALIDOS)('trocar o último dígito de %s invalida', (documento) => {
    const ultimo = Number(documento.at(-1))
    const trocado = documento.slice(0, -1) + String((ultimo + 1) % 10)

    expect(documentoValido(trocado)).toBe(false)
  })

  it('tira a máscara e passa para maiúsculas', () => {
    expect(normalizarDocumento('12.abc.345/01de-35')).toBe('12ABC34501DE35')
    expect(normalizarDocumento(' 529.982.247-25 ')).toBe('52998224725')
  })

  it.each(['11111111111', '00000000000000', '1234567890', '5299822472A', '12ABC34501DEAB'])(
    '%s não é válido',
    (documento) => {
      expect(documentoValido(documento)).toBe(false)
    },
  )
})
