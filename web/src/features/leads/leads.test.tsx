import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { admin, atendente, comSessao } from '@/test/api'
import { comCadastros, comLista, leadResumo } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'

async function tabela() {
  return within(await screen.findByRole('table'))
}

function ultimoPedido(pedidos: URL[]) {
  return Object.fromEntries(pedidos.at(-1)!.searchParams)
}

describe('lista de leads', () => {
  it('mostra etapa, origem, campanha, responsável e a entrada no horário de Brasília', async () => {
    comSessao(admin)
    comCadastros()
    comLista([
      leadResumo(),
      leadResumo({
        id: 2,
        nome: 'João Teste',
        teste: true,
        responsavel: null,
        etapa: { id: 6, nome: 'Perdido', tipo: 'perdido' },
      }),
    ])

    renderizarApp('/leads')

    const maria = within((await tabela()).getByText('Maria Souza').closest('tr')!)
    expect(maria.getByRole('link', { name: 'Maria Souza' })).toHaveAttribute('href', '/leads/1024')
    expect(maria.getByText('Novo')).toBeInTheDocument()
    expect(maria.getByText('Meta (Facebook e Instagram)')).toBeInTheDocument()
    expect(maria.getByText('Outubro | Serviço Y')).toBeInTheDocument()
    expect(maria.getByText('Adriana')).toBeInTheDocument()
    expect(maria.getByText('05/10/2026, 14:03')).toBeInTheDocument()
    const joao = within((await tabela()).getByText('João Teste').closest('tr')!)
    expect(joao.getByText('Teste')).toBeInTheDocument()
    expect(joao.getByText('Sem responsável')).toBeInTheDocument()
    expect(screen.getByText('2 leads')).toBeInTheDocument()
  })

  it('no celular, cada lead é um cartão que abre a ficha', async () => {
    comSessao(atendente)
    comCadastros()
    comLista([leadResumo()])

    renderizarApp('/leads')

    const cartoes = await screen.findByRole('list', { name: 'Leads' })
    expect(within(cartoes).getByRole('link')).toHaveAttribute('href', '/leads/1024')
  })

  it('a atendente não vê o filtro nem a coluna de responsável', async () => {
    comSessao(atendente)
    comCadastros()
    comLista([leadResumo()])

    renderizarApp('/leads')

    await tabela()
    expect(screen.queryByLabelText('Responsável')).not.toBeInTheDocument()
    expect(screen.queryByRole('columnheader', { name: 'Responsável' })).not.toBeInTheDocument()
  })

  it('os filtros vão para a API e ficam no endereço', async () => {
    comSessao(admin)
    comCadastros()
    const pedidos = comLista([leadResumo()])
    const { usuario, router } = renderizarApp('/leads')
    await tabela()

    await usuario.selectOptions(await screen.findByLabelText('Etapa'), 'Perdido')
    await usuario.selectOptions(screen.getByLabelText('Origem'), 'Google Ads')
    await usuario.selectOptions(screen.getByLabelText('Responsável'), 'Sem responsável')
    await usuario.type(screen.getByLabelText('Buscar leads'), 'maria{Enter}')

    await waitFor(() =>
      expect(ultimoPedido(pedidos)).toEqual({
        etapaId: '6',
        origem: 'google_ads',
        semResponsavel: 'true',
        busca: 'maria',
      }),
    )
    expect(Object.fromEntries(new URLSearchParams(router.state.location.search))).toEqual({
      etapa: '6',
      origem: 'google_ads',
      responsavel: 'sem',
      busca: 'maria',
    })
  })

  it('a responsável desativada aparece marcada no filtro', async () => {
    comSessao(admin)
    comCadastros()
    comLista([leadResumo()])

    renderizarApp('/leads')

    expect(await screen.findByRole('option', { name: 'Pietra (desativado)' })).toBeInTheDocument()
  })

  it('começa com os filtros do endereço e limpa todos', async () => {
    comSessao(atendente)
    comCadastros()
    const pedidos = comLista([leadResumo()])
    const { usuario, router } = renderizarApp('/leads?origem=site&busca=joao&pagina=2')

    await tabela()
    expect(ultimoPedido(pedidos)).toEqual({ origem: 'site', busca: 'joao', pagina: '2' })
    expect(screen.getByLabelText('Origem')).toHaveValue('site')
    expect(screen.getByLabelText('Buscar leads')).toHaveValue('joao')

    await usuario.click(screen.getByRole('button', { name: 'Limpar filtros' }))

    await waitFor(() => expect(ultimoPedido(pedidos)).toEqual({}))
    expect(router.state.location.search).toBe('')
    expect(screen.getByLabelText('Buscar leads')).toHaveValue('')
  })

  it('pagina de 25 em 25', async () => {
    comSessao(atendente)
    comCadastros()
    const pedidos = comLista([leadResumo()], 30)
    const { usuario } = renderizarApp('/leads')

    expect(await screen.findByText('Página 1 de 2')).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Próxima' }))

    expect(await screen.findByText('Página 2 de 2')).toBeInTheDocument()
    expect(ultimoPedido(pedidos)).toEqual({ pagina: '2' })
    expect(screen.getByRole('button', { name: 'Próxima' })).toBeDisabled()
  })

  it('avisa quando nenhum lead passa nos filtros', async () => {
    comSessao(atendente)
    comCadastros()
    comLista([])

    renderizarApp('/leads')

    expect(await screen.findByText('Nenhum lead encontrado.')).toBeInTheDocument()
  })
})
