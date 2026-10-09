import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { hojeEmBrasilia, intervaloDoAtalho } from '@/features/painel/periodo'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comQuadro } from '@/test/leads'
import { comRelatorio, relatorio, relatorioVazio } from '@/test/relatorio'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

/** O texto de cada célula das linhas de um trecho da tabela (o corpo ou o rodapé). */
function celulas(trecho: HTMLElement) {
  return within(trecho)
    .getAllByRole('row')
    .map((linha) =>
      within(linha)
        .getAllByRole('cell')
        .map((celula) => celula.textContent),
    )
}

function itensDoMenu() {
  const menu = screen.getByRole('navigation', { name: 'Menu principal' })
  return within(menu)
    .getAllByRole('link')
    .map((link) => link.textContent)
}

function linkDoCsv() {
  return screen.getByRole('link', { name: 'Baixar CSV' })
}

describe('relatório por campanha', () => {
  it('mostra cada campanha, as sem campanha no fim e o total, no item Relatório do menu do admin', async () => {
    comSessao(admin)
    comRelatorio()

    renderizarApp('/relatorio')

    const tabela = await screen.findByRole('table', { name: 'Relatório por campanha' })
    expect(
      screen.getByRole('heading', { name: 'Relatório por campanha', level: 1 }),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Relatório' })).toHaveAttribute('aria-current', 'page')
    expect(itensDoMenu()).toEqual([
      'Painel',
      'Funil',
      'Leads',
      'Relatório',
      'Importação',
      'Auditoria',
      'Usuários',
      'Etapas e motivos',
    ])
    expect(screen.getByText('01/09/2026 a 30/09/2026')).toBeInTheDocument()

    const [, corpo, rodape] = within(tabela).getAllByRole('rowgroup')
    expect(celulas(corpo)).toEqual([
      [
        'Setembro | Serviço Y Meta (Facebook e Instagram)',
        '4',
        '4',
        '2',
        '1',
        '1',
        '25%',
        '30 min',
      ],
      ['Google | Serviço Z Google Ads', '2', '3', '0', '1', '1', '50%', '40 min'],
      ['Sem campanha (não veio de anúncio) Site', '1', '2', '1', '0', '0', '0%', '—'],
      ['Sem campanha (não veio de anúncio) Cadastro manual', '1', '1', '1', '0', '0', '0%', '—'],
    ])
    expect(celulas(rodape)).toEqual([['Total', '8', '10', '4', '2', '2', '25%', '30 min']])

    // No celular, os mesmos números em cartões, com o total no fim.
    const cartoes = within(
      screen.getByRole('list', { name: 'Relatório por campanha' }),
    ).getAllByRole('listitem')
    expect(cartoes).toHaveLength(5)
    expect(cartoes[4]).toHaveTextContent('Total')

    // Nos últimos 30 dias, o CSV usa o padrão da API, como a tela.
    expect(linkDoCsv()).toHaveAttribute('href', '/api/relatorios/campanhas/csv')
    expect(linkDoCsv()).toHaveAttribute('download')
  })

  it('manda o período para a API, para o endereço e para o link do CSV', async () => {
    comSessao(admin)
    const pedidos = comRelatorio()
    const { usuario, router } = renderizarApp('/relatorio')
    await screen.findByRole('table', { name: 'Relatório por campanha' })
    const ultimo = () => Object.fromEntries(pedidos.at(-1)!.searchParams)

    expect(ultimo()).toEqual({})

    await usuario.selectOptions(screen.getByLabelText('Período'), 'Últimos 7 dias')
    const semana = intervaloDoAtalho('7d', hojeEmBrasilia())
    await waitFor(() => expect(ultimo()).toEqual(semana))
    expect(router.state.location.search).toBe('?periodo=7d')
    expect(linkDoCsv()).toHaveAttribute(
      'href',
      `/api/relatorios/campanhas/csv?de=${semana.de}&ate=${semana.ate}`,
    )

    // O personalizado começa com o período que está na tela.
    await usuario.selectOptions(screen.getByLabelText('Período'), 'Personalizado')
    expect(await screen.findByLabelText('De')).toHaveValue('2026-09-01')
    expect(screen.getByLabelText('Até')).toHaveValue('2026-09-30')
    await waitFor(() => expect(ultimo()).toEqual({ de: '2026-09-01', ate: '2026-09-30' }))
    expect(linkDoCsv()).toHaveAttribute(
      'href',
      '/api/relatorios/campanhas/csv?de=2026-09-01&ate=2026-09-30',
    )
  })

  it('a atendente não tem o relatório: nem no menu, nem pelo endereço, que leva ao funil', async () => {
    comSessao(atendente)
    comQuadro()

    const { router } = renderizarApp('/relatorio')

    await waitFor(() => expect(router.state.location.pathname).toBe('/funil'))
    expect(itensDoMenu()).toEqual(['Funil', 'Leads', 'Painel'])
  })

  it('sem leads novos nem entradas no período, avisa', async () => {
    comSessao(admin)
    comRelatorio(relatorioVazio())

    renderizarApp('/relatorio')

    expect(
      await screen.findByText('Nenhuma campanha teve leads novos ou entradas no período.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('num período invertido, mostra a mensagem da API e não oferece o CSV', async () => {
    comSessao(admin)
    servidor.use(
      http.get('*/api/relatorios/campanhas', () =>
        problema(400, { errors: { ate: ['O fim do período vem depois do início.'] } }),
      ),
    )

    renderizarApp('/relatorio?de=2026-09-30&ate=2026-09-01')

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'O fim do período vem depois do início.',
    )
    expect(screen.queryByRole('link', { name: 'Baixar CSV' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Tentar de novo' })).not.toBeInTheDocument()
  })

  it('numa falha, oferece tentar de novo', async () => {
    comSessao(admin)
    let falhar = true
    servidor.use(
      http.get('*/api/relatorios/campanhas', () =>
        falhar ? problema(404) : HttpResponse.json(relatorio()),
      ),
    )
    const { usuario } = renderizarApp('/relatorio')

    expect(await screen.findByText('Não foi possível carregar o relatório.')).toBeInTheDocument()
    falhar = false
    await usuario.click(screen.getByRole('button', { name: 'Tentar de novo' }))

    expect(await screen.findByRole('table', { name: 'Relatório por campanha' })).toBeInTheDocument()
  })

  it('recarrega quando um lead muda, pelo tempo real', async () => {
    comSessao(admin)
    const pedidos = comRelatorio()
    const { hub } = renderizarApp('/relatorio')
    await screen.findByRole('table', { name: 'Relatório por campanha' })
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    hub.emitir('LeadsAlterados', [1024])

    await waitFor(() => expect(pedidos).toHaveLength(2))
  })
})
