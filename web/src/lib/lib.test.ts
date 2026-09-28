import { describe, expect, it } from 'vitest'
import { formatarDataHora, formatarHora } from './datas'
import { gerarSenha, TAMANHO_MINIMO_SENHA } from './senha'

describe('gerarSenha', () => {
  it('gera três blocos de quatro caracteres fáceis de digitar', () => {
    const senha = gerarSenha()

    expect(senha).toMatch(/^[a-hjkmnp-z2-9]{4}-[a-hjkmnp-z2-9]{4}-[a-hjkmnp-z2-9]{4}$/)
    expect(senha.length).toBeGreaterThanOrEqual(TAMANHO_MINIMO_SENHA)
  })

  it('gera uma senha diferente a cada vez', () => {
    const senhas = new Set(Array.from({ length: 50 }, gerarSenha))

    expect(senhas.size).toBe(50)
  })
})

describe('datas', () => {
  it('mostra no horário de Brasília as datas que a API manda em UTC', () => {
    expect(formatarHora('2026-10-05T17:32:00Z')).toBe('14:32')
    expect(formatarDataHora('2026-10-06T01:15:00Z')).toBe('05/10/2026, 22:15')
  })
})
