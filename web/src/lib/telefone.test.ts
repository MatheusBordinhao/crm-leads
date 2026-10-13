import { describe, expect, it } from 'vitest'
import { formatarTelefone } from './telefone'

describe('telefone na tela', () => {
  it.each([
    ['+5551996680475', '(51) 99668-0475'],
    ['+5511987654321', '(11) 98765-4321'],
    ['+555133334444', '(51) 3333-4444'],
  ])('o do Brasil, %s, sai no formato nacional', (guardado, naTela) => {
    expect(formatarTelefone(guardado)).toBe(naTela)
  })

  it.each([
    // De outro país: continua como está guardado.
    '+16502530000',
    '+351912345678',
    // Não reconhecido: a API guarda como veio, e a ficha mostra o aviso.
    '(51) 9999-888',
    '123',
    // 0800 não tem DDD: o libphonenumber guarda o 800 depois do 55.
    '+558001234567',
  ])('%s fica como está', (guardado) => {
    expect(formatarTelefone(guardado)).toBe(guardado)
  })
})
