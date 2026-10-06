import { describe, expect, it } from 'vitest'
import { ErroApi } from '@/api/cliente'
import {
  formatarDataHora,
  formatarDia,
  formatarDuracao,
  formatarHora,
  paraCampoDataHora,
  tempoDesde,
} from './datas'
import { mensagemDoErro } from './formularios'
import { formatarNumero, formatarTaxa } from './numeros'
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

  it('diz quanto tempo passou desde a entrada', () => {
    const agora = new Date('2026-10-05T17:00:00Z')

    expect(tempoDesde('2026-10-05T16:59:30Z', agora)).toBe('agora')
    expect(tempoDesde('2026-10-05T16:55:00Z', agora)).toBe('há 5 min')
    expect(tempoDesde('2026-10-05T14:00:00Z', agora)).toBe('há 3 h')
    expect(tempoDesde('2026-10-04T16:00:00Z', agora)).toBe('ontem')
    expect(tempoDesde('2026-09-23T17:00:00Z', agora)).toBe('há 12 dias')
  })

  it('monta o valor do campo de data e hora no fuso do navegador', () => {
    expect(paraCampoDataHora(new Date(2026, 9, 5, 9, 3))).toBe('2026-10-05T09:03')
  })

  it('mostra um dia sem hora como a tela, sem mudar de fuso', () => {
    expect(formatarDia('2026-09-07')).toBe('07/09/2026')
  })

  it('diz uma duração em minutos, horas e dias', () => {
    expect(formatarDuracao(0)).toBe('menos de 1 min')
    expect(formatarDuracao(0.6)).toBe('menos de 1 min')
    expect(formatarDuracao(44.6)).toBe('45 min')
    expect(formatarDuracao(59.6)).toBe('1 h')
    expect(formatarDuracao(75)).toBe('1 h 15 min')
    expect(formatarDuracao(24 * 60)).toBe('1 dia')
    expect(formatarDuracao(2 * 24 * 60 + 3 * 60 + 20)).toBe('2 dias e 3 h')
  })
})

describe('números', () => {
  it('mostra os números e as taxas no formato brasileiro', () => {
    expect(formatarNumero(1234)).toBe('1.234')
    expect(formatarTaxa(0.25)).toBe('25%')
    expect(formatarTaxa(1 / 3)).toBe('33,3%')
    expect(formatarTaxa(0)).toBe('0%')
    expect(formatarTaxa(null)).toBe('—')
  })
})

describe('mensagemDoErro', () => {
  it('usa a mensagem do campo, depois o detalhe, e avisa quando não há conexão', () => {
    const comCampo = new ErroApi(400, {
      errors: { etapaId: ['Etapa não encontrada ou desativada.'] },
    })
    const comDetalhe = new ErroApi(400, { detail: 'Para marcar como Ganho, informe um CPF.' })

    expect(mensagemDoErro(comCampo)).toBe('Etapa não encontrada ou desativada.')
    expect(mensagemDoErro(comDetalhe)).toBe('Para marcar como Ganho, informe um CPF.')
    expect(mensagemDoErro(new TypeError('Failed to fetch'))).toMatch(/Não foi possível falar/)
  })
})
