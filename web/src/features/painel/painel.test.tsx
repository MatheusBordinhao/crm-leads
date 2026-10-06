import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comPainel, painel, painelVazio } from '@/test/painel'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'
import { hojeEmBrasilia, intervaloDoAtalho } from './periodo'

/** O texto de cada linha de uma lista de barras: o nome, o número e o detalhe. */
async function linhas(lista: string) {
  const elemento = await screen.findByRole('list', { name: lista })
  return within(elemento)
    .getAllByRole('listitem')
    .map((item) => item.textContent)
}

function indicador(nome: string) {
  return within(screen.getByRole('group', { name: nome }))
}

function itensDoMenu() {
  const menu = screen.getByRole('navigation', { name: 'Menu principal' })
  return within(menu)
    .getAllByRole('link')
    .map((link) => link.textContent)
}

describe('painel', () => {
  it('a tela inicial do admin é o painel, o primeiro item do menu', async () => {
    comSessao(admin)
    comPainel()

    const { router } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Painel', level: 1 })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/painel')
    expect(itensDoMenu()).toEqual([
      'Painel',
      'Funil',
      'Leads',
      'Importação',
      'Usuários',
      'Etapas e motivos',
    ])
  })

  it('mostra os números, as barras e a tabela por atendente', async () => {
    comSessao(admin)
    comPainel()

    renderizarApp('/painel')

    expect(await screen.findByText('01/09/2026 a 30/09/2026')).toBeInTheDocument()
    expect(indicador('Leads novos').getByText('8')).toBeInTheDocument()
    expect(indicador('Entradas').getByText('10')).toBeInTheDocument()
    expect(indicador('Conversão').getByText('25%')).toBeInTheDocument()
    expect(indicador('Conversão').getByText('2 de 8 em Ganho')).toBeInTheDocument()
    expect(indicador('Primeiro contato').getByText('30 min')).toBeInTheDocument()
    expect(indicador('Primeiro contato').getByText('mediana · 3 sem contato')).toBeInTheDocument()
    expect(await linhas('Situação atual')).toEqual([
      'Novo 3',
      'Em contato 1',
      'Qualificado 0',
      'Proposta enviada 0',
      'Ganho 2',
      'Perdido 2',
    ])
    expect(await linhas('Por origem')).toEqual([
      'Meta (Facebook e Instagram) 4 1 ganho · 25%',
      'Google Ads 2 1 ganho · 50%',
      'Site 1 0 ganhos · 0%',
      'Cadastro manual 1 0 ganhos · 0%',
    ])
    expect(await linhas('Por campanha')).toEqual([
      'Outubro | Serviço Y 4 1 ganho · 25%',
      'Google | Serviço Z 2 1 ganho · 50%',
      'Sem campanha 2 0 ganhos · 0%',
    ])
    expect(await linhas('Motivos de perda')).toEqual([
      'Preço 1',
      'Sem interesse 1',
      'Sem resposta 1',
    ])
    expect(
      screen.getByText('3 leads foram para Perdido no período, de qualquer data de entrada.'),
    ).toBeInTheDocument()

    const tabela = screen.getByRole('table', { name: 'Por atendente' })
    const celulas = within(tabela)
      .getAllByRole('row')
      .slice(1)
      .map((linha) =>
        within(linha)
          .getAllByRole('cell')
          .map((celula) => celula.textContent),
      )
    expect(celulas).toEqual([
      ['Adriana', '5', '1', '20%', '45 min'],
      ['Pietra (desativado)', '2', '1', '50%', '20 min'],
      ['Sem responsável', '1', '0', '0%', '—'],
    ])
  })

  it('a atendente vê os próprios números, sem o filtro de responsável nem a tabela por atendente', async () => {
    comSessao(atendente)
    comPainel(painel({ porAtendente: null }))

    renderizarApp('/painel')

    expect(await screen.findByRole('list', { name: 'Situação atual' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Responsável')).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Por atendente' })).not.toBeInTheDocument()
    expect(itensDoMenu()).toEqual(['Funil', 'Leads', 'Painel'])
  })

  it('manda o período e os filtros para a API e os guarda no endereço', async () => {
    comSessao(admin)
    const pedidos = comPainel()
    const { usuario, router } = renderizarApp('/painel')
    await screen.findByRole('list', { name: 'Situação atual' })
    const ultimo = () => Object.fromEntries(pedidos.at(-1)!.searchParams)

    // Nos últimos 30 dias, vale o padrão da API.
    expect(ultimo()).toEqual({})

    await usuario.selectOptions(screen.getByLabelText('Período'), 'Últimos 7 dias')
    await waitFor(() => expect(ultimo()).toEqual(intervaloDoAtalho('7d', hojeEmBrasilia())))
    expect(router.state.location.search).toBe('?periodo=7d')

    // O personalizado começa com o período que está na tela.
    await usuario.selectOptions(screen.getByLabelText('Período'), 'Personalizado')
    expect(await screen.findByLabelText('De')).toHaveValue('2026-09-01')
    expect(screen.getByLabelText('Até')).toHaveValue('2026-09-30')
    await waitFor(() => expect(ultimo()).toEqual({ de: '2026-09-01', ate: '2026-09-30' }))

    await usuario.selectOptions(screen.getByLabelText('Origem'), 'Google Ads')
    await usuario.selectOptions(
      screen.getByLabelText('Campanha'),
      await screen.findByRole('option', { name: 'Outubro | Serviço Y' }),
    )
    await usuario.selectOptions(screen.getByLabelText('Responsável'), 'Sem responsável')
    await waitFor(() =>
      expect(ultimo()).toEqual({
        de: '2026-09-01',
        ate: '2026-09-30',
        origem: 'google_ads',
        campanhaId: '10',
        semResponsavel: 'true',
      }),
    )

    await usuario.click(screen.getByRole('button', { name: 'Limpar filtros' }))
    await waitFor(() => expect(ultimo()).toEqual({}))
    expect(router.state.location.search).toBe('')
  })

  it('sem leads no período, mostra os vazios e nenhuma taxa', async () => {
    comSessao(admin)
    comPainel(painelVazio())

    renderizarApp('/painel')

    expect(await screen.findByText('Nenhum lead foi para Perdido no período.')).toBeInTheDocument()
    expect(indicador('Conversão').getByText('—')).toBeInTheDocument()
    expect(indicador('Conversão').getByText('sem leads novos')).toBeInTheDocument()
    expect(indicador('Primeiro contato').getByText('—')).toBeInTheDocument()
    expect(screen.getAllByText('Nenhum lead novo no período.')).toHaveLength(4)
    expect(screen.queryByRole('list', { name: 'Situação atual' })).not.toBeInTheDocument()
  })

  it('num período invertido, mostra a mensagem da API', async () => {
    comSessao(admin)
    comPainel()
    servidor.use(
      http.get('*/api/painel', () =>
        problema(400, { errors: { ate: ['O fim do período vem depois do início.'] } }),
      ),
    )

    renderizarApp('/painel?de=2026-09-30&ate=2026-09-01')

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O fim do período vem depois do início.',
    )
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument()
  })

  it('numa falha, oferece tentar de novo', async () => {
    comSessao(admin)
    comPainel()
    let falhar = true
    servidor.use(
      http.get('*/api/painel', () => (falhar ? problema(404) : HttpResponse.json(painel()))),
    )
    const { usuario } = renderizarApp('/painel')

    expect(await screen.findByText('Não foi possível carregar o painel.')).toBeInTheDocument()
    falhar = false
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByRole('list', { name: 'Situação atual' })).toBeInTheDocument()
  })
})
