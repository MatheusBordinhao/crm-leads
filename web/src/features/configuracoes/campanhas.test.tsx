import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { CampanhaResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comQuadro } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

type Pedido = { caminho: string; corpo: unknown }

/**
 * API simulada com estado: o nome novo aparece na lista recarregada. Devolve os pedidos de renomeação. A `recusa`
 * responde no lugar da API, para os testes de erro.
 */
function comCampanhas(recusa?: () => Response) {
  let lista: CampanhaResponse[] = [
    {
      id: 10,
      origem: 'meta_lead_ads',
      idExterno: '120210000000001',
      nome: 'Meta | Captação outubro',
      nomeProvisorio: false,
    },
    {
      id: 11,
      origem: 'google_ads',
      idExterno: '20000000001',
      nome: 'Google Ads · campanha 20000000001',
      nomeProvisorio: true,
    },
  ]
  const pedidos: Pedido[] = []
  comSessao(admin)
  servidor.use(
    http.get('*/api/campanhas', () => HttpResponse.json(lista)),
    http.put('*/api/campanhas/:id', async ({ request, params }) => {
      const corpo = (await request.json()) as { nome: string }
      pedidos.push({ caminho: new URL(request.url).pathname, corpo })
      if (recusa) {
        return recusa()
      }
      lista = lista.map((campanha) =>
        campanha.id === Number(params.id)
          ? { ...campanha, nome: corpo.nome, nomeProvisorio: false }
          : campanha,
      )
      return HttpResponse.json(lista.find((campanha) => campanha.id === Number(params.id)))
    }),
  )
  return pedidos
}

async function listaDeCampanhas() {
  return within(await screen.findByRole('list', { name: 'Campanhas' }))
}

describe('campanhas', () => {
  it('a atendente não tem o item no menu e, pelo endereço, volta ao funil', async () => {
    comSessao(atendente)
    comQuadro()

    const { router } = renderizarApp('/configuracoes/campanhas')

    // Como 1º teste do arquivo, ele paga a subida fria, com dois redirecionamentos. Por isso, primeiro o caminho, numa
    // espera que não consulta o DOM, e depois o título, pelo texto dentro do h1: a consulta por papel no funil custa
    // mais a cada tentativa, como em "a tela inicial do admin é o painel".
    await waitFor(() => expect(router.state.location.pathname).toBe('/funil'))
    await waitFor(() => expect(screen.queryByText('Funil', { selector: 'h1' })).toBeInTheDocument())
    const menu = screen.getByRole('navigation', { name: 'Menu principal' })
    expect(within(menu).queryByRole('link', { name: 'Campanhas' })).not.toBeInTheDocument()
  })

  it('o admin chega pelo menu e vê as provisórias primeiro, com a marca', async () => {
    // O quadro registra as campanhas padrão; as deste teste vêm depois, para valerem.
    comQuadro()
    comCampanhas()
    const { usuario } = renderizarApp('/funil')

    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    await usuario.click(within(menu).getByRole('link', { name: 'Campanhas' }))

    const itens = (await listaDeCampanhas()).getAllByRole('listitem')
    expect(itens.map((item) => item.textContent)).toEqual([
      expect.stringContaining('Google Ads · campanha 20000000001'),
      expect.stringContaining('Meta | Captação outubro'),
    ])
    expect(within(itens[0]).getByText('Nome provisório')).toBeInTheDocument()
    expect(within(itens[0]).getByText('Google Ads · id 20000000001')).toBeInTheDocument()
    expect(within(itens[1]).queryByText('Nome provisório')).not.toBeInTheDocument()
  })

  it('renomeia a campanha provisória, que perde a marca', async () => {
    const pedidos = comCampanhas()
    const { usuario } = renderizarApp('/configuracoes/campanhas')

    await usuario.click(
      (await listaDeCampanhas()).getByRole('button', {
        name: 'Renomear Google Ads · campanha 20000000001',
      }),
    )
    const dialogo = within(
      await screen.findByRole('dialog', { name: 'Renomear Google Ads · campanha 20000000001' }),
    )
    await usuario.clear(dialogo.getByLabelText('Nome'))
    await usuario.type(dialogo.getByLabelText('Nome'), 'Google | Pesquisa outubro')
    await usuario.click(dialogo.getByRole('button', { name: 'Salvar' }))

    expect(
      await screen.findByText('Campanha renomeada para Google | Pesquisa outubro.'),
    ).toBeInTheDocument()
    const lista = await listaDeCampanhas()
    expect(await lista.findByText('Google | Pesquisa outubro')).toBeInTheDocument()
    expect(lista.queryByText('Nome provisório')).not.toBeInTheDocument()
    expect(pedidos).toEqual([
      { caminho: '/api/campanhas/11', corpo: { nome: 'Google | Pesquisa outubro' } },
    ])
  })

  it('o erro da API aparece no campo, e o diálogo continua aberto', async () => {
    comCampanhas(() => problema(400, { errors: { nome: ['O nome pode ter até 200 caracteres.'] } }))
    const { usuario } = renderizarApp('/configuracoes/campanhas')

    await usuario.click(
      (await listaDeCampanhas()).getByRole('button', { name: 'Renomear Meta | Captação outubro' }),
    )
    const dialogo = within(
      await screen.findByRole('dialog', { name: 'Renomear Meta | Captação outubro' }),
    )
    await usuario.type(dialogo.getByLabelText('Nome'), ' 2')
    await usuario.click(dialogo.getByRole('button', { name: 'Salvar' }))

    expect(await dialogo.findByText('O nome pode ter até 200 caracteres.')).toBeInTheDocument()
    expect(dialogo.getByLabelText('Nome')).toHaveValue('Meta | Captação outubro 2')
  })
})
