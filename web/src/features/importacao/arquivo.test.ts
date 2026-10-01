import { describe, expect, it } from 'vitest'
import { lerEmBase64 } from './arquivo'

describe('arquivo da importação', () => {
  it('vai em base64, byte a byte, sem mudar a codificação', async () => {
    // "nome;ação" em Windows-1252: o ç e o ã são um byte só e precisam chegar como vieram.
    const windows1252 = new Uint8Array([0x6e, 0x6f, 0x6d, 0x65, 0x3b, 0x61, 0xe7, 0xe3, 0x6f])

    const conteudo = await lerEmBase64(new File([windows1252], 'planilha.csv'))

    expect(conteudo).toBe('bm9tZTth5+Nv')
  })

  it('arquivo vazio vira texto vazio', async () => {
    expect(await lerEmBase64(new File([], 'vazio.csv'))).toBe('')
  })
})
