import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { LeadResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comCadastros, comLista, fichaLead } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

const anonimizado = fichaLead({
  nome: 'Titular removido',
  email: null,
  telefone: null,
  empresa: null,
  respostasExtras: [],
  anonimizadoEm: '2026-10-09T15:00:00Z',
  versao: 778,
})

/** Ficha simulada, com a linha do tempo vazia e os cadastros. */
function comFicha(lead: LeadResponse = fichaLead()) {
  comCadastros()
  servidor.use(
    http.get('*/api/leads/:id', () => HttpResponse.json(lead)),
    http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
  )
}

/** Anonimização simulada: devolve a ficha anonimizada. Devolve os ids pedidos. */
function comAnonimizacao() {
  const pedidos: string[] = []
  servidor.use(
    http.post('*/api/leads/:id/anonimizar', ({ params }) => {
      pedidos.push(String(params.id))
      return HttpResponse.json(anonimizado)
    }),
  )
  return pedidos
}

/** O cartão Privacidade da ficha, que o CardTitle marca como um título visual, e não como heading. */
async function cartaoPrivacidade() {
  const titulo = await screen.findByText('Privacidade')
  return within(titulo.closest<HTMLElement>('[data-slot="card"]')!)
}

describe('privacidade do lead', () => {
  it('o admin exporta os dados do titular, anonimiza e abre a auditoria do lead', async () => {
    comSessao(admin)
    comFicha()

    renderizarApp('/leads/1024')

    const cartao = await cartaoPrivacidade()
    const exportar = cartao.getByRole('link', { name: 'Exportar dados' })
    expect(exportar).toHaveAttribute('href', '/api/leads/1024/dados-titular')
    expect(exportar).toHaveAttribute('download')
    expect(cartao.getByRole('button', { name: 'Anonimizar' })).toBeInTheDocument()
    expect(cartao.getByRole('link', { name: 'Ver na auditoria' })).toHaveAttribute(
      'href',
      '/auditoria?lead=1024',
    )
    expect(cartao.queryByRole('button', { name: 'Excluir lead de teste' })).not.toBeInTheDocument()
  })

  it('a atendente não vê o cartão', async () => {
    comSessao(atendente)
    comFicha()

    renderizarApp('/leads/1024')

    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    expect(screen.queryByText('Privacidade')).not.toBeInTheDocument()
  })

  it('anonimizar pede confirmação, e cancelar não chama a API', async () => {
    comSessao(admin)
    comFicha()
    const pedidos = comAnonimizacao()
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.click((await cartaoPrivacidade()).getByRole('button', { name: 'Anonimizar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anonimizar Maria Souza?' })
    expect(within(dialogo).getByText('Isto não tem volta.')).toBeInTheDocument()
    expect(
      within(dialogo).getByText(/Se o titular pediu uma cópia dos dados, exporte antes/),
    ).toBeInTheDocument()
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(pedidos).toEqual([])
    expect(screen.getByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
  })

  it('confirmado, a ficha mostra o aviso no lugar dos dados, sem contato nem interação', async () => {
    comSessao(admin)
    comFicha()
    const pedidos = comAnonimizacao()
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.click((await cartaoPrivacidade()).getByRole('button', { name: 'Anonimizar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anonimizar Maria Souza?' })
    await usuario.click(within(dialogo).getByRole('button', { name: 'Anonimizar' }))

    expect(await screen.findByText('Lead anonimizado.')).toBeInTheDocument()
    expect(pedidos).toEqual(['1024'])
    expect(await screen.findByRole('heading', { name: 'Titular removido' })).toBeInTheDocument()
    expect(
      screen.getByText(/dados pessoais deste lead foram removidos em 09\/10\/2026, 12:00/),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar interação' })).not.toBeInTheDocument()
    const cartao = await cartaoPrivacidade()
    expect(
      cartao.getByText('Os dados pessoais foram removidos em 09/10/2026, 12:00.'),
    ).toBeInTheDocument()
    expect(cartao.queryByRole('button', { name: 'Anonimizar' })).not.toBeInTheDocument()
    expect(cartao.getByRole('link', { name: 'Exportar dados' })).toBeInTheDocument()
  })

  it('com uma importação em andamento, mostra a mensagem da API e o lead fica como estava', async () => {
    comSessao(admin)
    comFicha()
    const mensagem =
      'Há uma importação em andamento. Tente de novo quando ela terminar: o arquivo dela ainda guarda os dados das pessoas.'
    servidor.use(http.post('*/api/leads/:id/anonimizar', () => problema(409, { detail: mensagem })))
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.click((await cartaoPrivacidade()).getByRole('button', { name: 'Anonimizar' }))
    const dialogo = await screen.findByRole('dialog', { name: 'Anonimizar Maria Souza?' })
    await usuario.click(within(dialogo).getByRole('button', { name: 'Anonimizar' }))

    expect(await within(dialogo).findByText(mensagem)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Maria Souza', hidden: true })).toBeInTheDocument()
  })

  it('a ficha anonimizada, para a atendente, mostra o aviso no lugar do formulário', async () => {
    comSessao(atendente)
    comFicha(anonimizado)

    renderizarApp('/leads/1024')

    expect(await screen.findByRole('heading', { name: 'Titular removido' })).toBeInTheDocument()
    expect(screen.getByText(/não aceita edição nem interações novas/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Salvar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar interação' })).not.toBeInTheDocument()
  })

  it('o lead de teste é excluído depois da confirmação, e a tela volta para a lista', async () => {
    comSessao(admin)
    comFicha(fichaLead({ teste: true, responsavel: null }))
    comLista([])
    const exclusoes: string[] = []
    servidor.use(
      http.delete('*/api/leads/:id', ({ params }) => {
        exclusoes.push(String(params.id))
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { usuario, router } = renderizarApp('/leads/1024')

    const cartao = await cartaoPrivacidade()
    expect(cartao.queryByRole('button', { name: 'Anonimizar' })).not.toBeInTheDocument()
    await usuario.click(cartao.getByRole('button', { name: 'Excluir lead de teste' }))
    const dialogo = await screen.findByRole('dialog', {
      name: 'Excluir o lead de teste Maria Souza?',
    })
    await usuario.click(within(dialogo).getByRole('button', { name: 'Excluir' }))

    expect(await screen.findByText('Lead de teste excluído.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/leads')
    expect(exclusoes).toEqual(['1024'])
  })
})
