import { describe, expect, it } from 'vitest'
import { hojeEmBrasilia, intervaloDoAtalho, intervaloDoEndereco, lerPeriodo } from './periodo'

describe('período do painel', () => {
  it('conta o hoje no horário de Brasília, mesmo quando já é o dia seguinte em UTC', () => {
    expect(hojeEmBrasilia(new Date('2026-10-06T02:30:00Z'))).toBe('2026-10-05')
    expect(hojeEmBrasilia(new Date('2026-10-06T03:00:00Z'))).toBe('2026-10-06')
  })

  it('monta os atalhos com o hoje incluído, atravessando a virada do mês e do ano', () => {
    expect(intervaloDoAtalho('7d', '2026-10-03')).toEqual({ de: '2026-09-27', ate: '2026-10-03' })
    expect(intervaloDoAtalho('30d', '2026-10-06')).toEqual({ de: '2026-09-07', ate: '2026-10-06' })
    expect(intervaloDoAtalho('90d', '2027-01-15')).toEqual({ de: '2026-10-18', ate: '2027-01-15' })
    expect(intervaloDoAtalho('mes', '2026-10-06')).toEqual({ de: '2026-10-01', ate: '2026-10-06' })
    expect(intervaloDoAtalho('mes-passado', '2027-01-15')).toEqual({
      de: '2026-12-01',
      ate: '2026-12-31',
    })
    expect(intervaloDoAtalho('mes-passado', '2028-03-31')).toEqual({
      de: '2028-02-01',
      ate: '2028-02-29',
    })
  })

  it('lê do endereço o atalho ou as datas do personalizado, e nos 30 dias deixa o padrão da API', () => {
    const hoje = '2026-10-06'
    const endereco = (busca: string) => new URLSearchParams(busca)

    expect(lerPeriodo(endereco(''))).toBe('30d')
    expect(intervaloDoEndereco(endereco(''), hoje)).toEqual({})
    expect(lerPeriodo(endereco('periodo=qualquer'))).toBe('30d')
    expect(intervaloDoEndereco(endereco('periodo=7d'), hoje)).toEqual({
      de: '2026-09-30',
      ate: hoje,
    })
    expect(lerPeriodo(endereco('periodo=7d&de=2026-09-01'))).toBe('personalizado')
    expect(intervaloDoEndereco(endereco('de=2026-09-01&ate=2026-09-30'), hoje)).toEqual({
      de: '2026-09-01',
      ate: '2026-09-30',
    })
    expect(intervaloDoEndereco(endereco('de=2026-09-01&ate=ontem'), hoje)).toEqual({
      de: '2026-09-01',
      ate: undefined,
    })
  })
})
