import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { CriarLeadResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comCadastros, comLista, fichaLead } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

/** Cadastro simulado. Devolve os corpos recebidos. */
function comCadastro(resposta: CriarLeadResponse, status = 201) {
  const recebidos: Record<string, unknown>[] = []
  servidor.use(
    http.post('*/api/leads', async ({ request }) => {
      recebidos.push((await request.json()) as Record<string, unknown>)
      return HttpResponse.json(resposta, { status })
    }),
    http.get('*/api/leads/:id', ({ params }) =>
      HttpResponse.json(fichaLead({ id: Number(params.id) })),
    ),
  )
  return recebidos
}

describe('novo lead', () => {
  it('abre pelo botão da lista', async () => {
    comSessao(atendente)
    comCadastros()
    comLista([])
    const { usuario } = renderizarApp('/leads')

    await usuario.click(await screen.findByRole('link', { name: 'Novo lead' }))

    expect(await screen.findByRole('heading', { name: 'Novo lead' })).toBeInTheDocument()
  })

  it('confere os campos antes de chamar a API', async () => {
    comSessao(atendente)
    const recebidos = comCadastro({ leadId: 1, situacao: 'criado', podeAbrir: true })
    const { usuario } = renderizarApp('/leads/novo')

    await usuario.type(await screen.findByLabelText('CPF ou CNPJ'), '529.982.247-26')
    await usuario.type(screen.getByLabelText('E-mail'), 'maria@sem-dominio')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    expect(await screen.findByText('Informe o nome.')).toBeInTheDocument()
    expect(screen.getByText('E-mail inválido.')).toBeInTheDocument()
    expect(screen.getByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
    expect(recebidos).toEqual([])
  })

  it('pede e-mail ou telefone', async () => {
    comSessao(atendente)
    comCadastro({ leadId: 1, situacao: 'criado', podeAbrir: true })
    const { usuario } = renderizarApp('/leads/novo')

    await usuario.type(await screen.findByLabelText('Nome'), 'Maria')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    expect(await screen.findByText('Informe o e-mail ou o telefone.')).toBeInTheDocument()
  })

  it('cadastra e abre a ficha', async () => {
    comSessao(atendente)
    const recebidos = comCadastro({ leadId: 55, situacao: 'criado', podeAbrir: true })
    const { usuario, router } = renderizarApp('/leads/novo')

    await usuario.type(await screen.findByLabelText('Nome'), 'João da Silva')
    await usuario.type(screen.getByLabelText('Telefone'), '(51) 99999-8888')
    await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '12.abc.345/01de-35')
    await usuario.selectOptions(screen.getByLabelText('UF'), 'RS')
    await usuario.selectOptions(screen.getByLabelText('Origem'), 'WhatsApp')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    expect(await screen.findByText('Lead cadastrado.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/leads/55')
    expect(recebidos).toEqual([
      {
        nome: 'João da Silva',
        email: null,
        telefone: '(51) 99999-8888',
        cpfCnpj: '12.abc.345/01de-35',
        uf: 'RS',
        empresa: null,
        origem: 'whatsapp',
        responsavelId: null,
      },
    ])
  })

  it('pessoa que já é lead de outra atendente: avisa sem dizer de quem e volta para a lista', async () => {
    comSessao(atendente)
    comCadastros()
    comLista([])
    comCadastro({ leadId: null, situacao: 'vinculado', podeAbrir: false }, 200)
    const { usuario, router } = renderizarApp('/leads/novo')

    await usuario.type(await screen.findByLabelText('Nome'), 'Maria')
    await usuario.type(screen.getByLabelText('E-mail'), 'maria@empresax.com.br')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    expect(await screen.findByText('Já existe um lead com esses dados.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/leads')
  })

  it('o admin escolhe a responsável entre os usuários ativos', async () => {
    comSessao(admin)
    comCadastros()
    const recebidos = comCadastro({ leadId: 8, situacao: 'criado', podeAbrir: true })
    const { usuario } = renderizarApp('/leads/novo')

    const responsavel = await screen.findByLabelText('Responsável')
    await within(responsavel).findByRole('option', { name: 'Adriana' })
    expect(within(responsavel).queryByRole('option', { name: 'Pietra' })).not.toBeInTheDocument()
    await usuario.selectOptions(responsavel, 'Adriana')
    await usuario.type(screen.getByLabelText('Nome'), 'Maria')
    await usuario.type(screen.getByLabelText('E-mail'), 'maria@empresax.com.br')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    await screen.findByText('Lead cadastrado.')
    expect(recebidos[0].responsavelId).toBe(atendente.id)
  })

  it('sem escolha do admin, o lead vai para o rodízio', async () => {
    comSessao(admin)
    comCadastros()
    const recebidos = comCadastro({ leadId: 9, situacao: 'criado', podeAbrir: true })
    const { usuario } = renderizarApp('/leads/novo')

    const responsavel = await screen.findByLabelText('Responsável')
    expect(responsavel).toHaveDisplayValue('Rodízio')
    await usuario.type(screen.getByLabelText('Nome'), 'Maria')
    await usuario.type(screen.getByLabelText('E-mail'), 'maria@empresax.com.br')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    await screen.findByText('Lead cadastrado.')
    expect(recebidos[0].responsavelId).toBeNull()
  })

  it('a atendente não escolhe a responsável', async () => {
    comSessao(atendente)

    renderizarApp('/leads/novo')

    expect(await screen.findByLabelText('Origem')).toBeInTheDocument()
    expect(screen.queryByLabelText('Responsável')).not.toBeInTheDocument()
  })

  it('mostra no campo o erro que a API devolve', async () => {
    comSessao(atendente)
    servidor.use(
      http.post('*/api/leads', () =>
        problema(400, {
          errors: { telefone: ['Telefone inválido. Informe com DDD, como (51) 99999-8888.'] },
        }),
      ),
    )
    const { usuario } = renderizarApp('/leads/novo')

    await usuario.type(await screen.findByLabelText('Nome'), 'Maria')
    await usuario.type(screen.getByLabelText('Telefone'), '(51) 0000-00000')
    await usuario.click(screen.getByRole('button', { name: 'Cadastrar lead' }))

    expect(
      await screen.findByText('Telefone inválido. Informe com DDD, como (51) 99999-8888.'),
    ).toBeInTheDocument()
  })
})
