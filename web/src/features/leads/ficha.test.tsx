import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { LeadResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { fichaLead } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

const comTelefoneNaoReconhecido = fichaLead({
  telefone: '(51) 9999-888',
  avisos: [
    { campo: 'telefone', mensagem: 'Telefone não reconhecido' },
    { campo: 'cpfCnpj', mensagem: 'Sem CPF/CNPJ' },
  ],
})

/** Ficha simulada. Devolve os corpos das edições recebidas. */
function comFicha(lead: LeadResponse = comTelefoneNaoReconhecido) {
  const edicoes: Record<string, unknown>[] = []
  servidor.use(
    http.get('*/api/leads/:id', () => HttpResponse.json(lead)),
    http.put('*/api/leads/:id', async ({ request }) => {
      const corpo = (await request.json()) as Record<string, unknown>
      edicoes.push(corpo)
      return HttpResponse.json({ ...lead, ...corpo, versao: lead.versao + 1 })
    }),
  )
  return edicoes
}

describe('ficha do lead', () => {
  it('mostra os dados, os avisos embaixo dos campos e a origem', async () => {
    comSessao(atendente)
    comFicha()

    renderizarApp('/leads/1024')

    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    expect(screen.getByLabelText('Telefone')).toHaveValue('(51) 9999-888')
    expect(screen.getByText('Telefone não reconhecido')).toBeInTheDocument()
    expect(screen.getByText('Sem CPF/CNPJ')).toBeInTheDocument()
    expect(screen.getByLabelText('UF')).toHaveValue('RS')
    expect(screen.getByText('Instagram')).toBeInTheDocument()
    expect(screen.getByText('Qual serviço você procura?')).toBeInTheDocument()
    expect(screen.getByText('Serviço Y')).toBeInTheDocument()
    expect(screen.getByText(/Entrou em 05\/10\/2026, 14:03/)).toBeInTheDocument()
    expect(screen.queryByText(/Responsável:/)).not.toBeInTheDocument()
  })

  it('o admin vê a responsável', async () => {
    comSessao(admin)
    comFicha(fichaLead({ responsavel: null }))

    renderizarApp('/leads/1024')

    expect(await screen.findByText('Sem responsável')).toBeInTheDocument()
  })

  it('salva com a versão, mesmo com o telefone inválido que veio da integração', async () => {
    comSessao(atendente)
    const edicoes = comFicha()
    const { usuario } = renderizarApp('/leads/1024')

    const empresa = await screen.findByLabelText('Empresa')
    await usuario.clear(empresa)
    await usuario.type(empresa, 'Empresa Nova')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Dados salvos.')).toBeInTheDocument()
    expect(edicoes).toEqual([
      {
        nome: 'Maria Souza',
        email: 'maria@empresax.com.br',
        telefone: '(51) 9999-888',
        cpfCnpj: null,
        uf: 'RS',
        empresa: 'Empresa Nova',
        versao: 777,
      },
    ])
  })

  it('confere o formato dos campos alterados antes de chamar a API', async () => {
    comSessao(atendente)
    const edicoes = comFicha()
    const { usuario } = renderizarApp('/leads/1024')

    const telefone = await screen.findByLabelText('Telefone')
    await usuario.clear(telefone)
    await usuario.type(telefone, '123')
    await usuario.type(screen.getByLabelText('CPF ou CNPJ'), '529.982.247-26')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(
      await screen.findByText('Telefone inválido. Informe com DDD, como (51) 99999-8888.'),
    ).toBeInTheDocument()
    expect(screen.getByText('CPF ou CNPJ inválido.')).toBeInTheDocument()
    expect(edicoes).toEqual([])
  })

  it('mostra no campo o erro que a API devolve', async () => {
    comSessao(atendente)
    comFicha()
    servidor.use(
      http.put('*/api/leads/:id', () =>
        problema(400, {
          errors: { telefone: ['Telefone inválido. Informe com DDD, como (51) 99999-8888.'] },
        }),
      ),
    )
    const { usuario } = renderizarApp('/leads/1024')

    const telefone = await screen.findByLabelText('Telefone')
    await usuario.clear(telefone)
    await usuario.type(telefone, '(51) 0000-00000')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(
      await screen.findByText('Telefone inválido. Informe com DDD, como (51) 99999-8888.'),
    ).toBeInTheDocument()
  })

  it('avisa quando outra pessoa alterou o lead e recarrega', async () => {
    comSessao(atendente)
    comFicha()
    servidor.use(
      http.put('*/api/leads/:id', () =>
        problema(409, {
          detail:
            'O lead foi alterado por outra pessoa. Recarregue a página para ver a versão atual.',
        }),
      ),
    )
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.type(await screen.findByLabelText('Empresa'), ' Ltda')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    const alerta = await screen.findByRole('alert')
    expect(within(alerta).getByText(/alterado por outra pessoa/)).toBeInTheDocument()
    servidor.use(
      http.get('*/api/leads/:id', () =>
        HttpResponse.json({ ...comTelefoneNaoReconhecido, empresa: 'Mudada', versao: 800 }),
      ),
    )
    await usuario.click(within(alerta).getByRole('button', { name: 'Recarregar' }))

    expect(await screen.findByDisplayValue('Mudada')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('lead de outra pessoa ou inexistente mostra "Lead não encontrado"', async () => {
    comSessao(atendente)
    servidor.use(http.get('*/api/leads/:id', () => problema(404)))

    renderizarApp('/leads/1024')

    expect(await screen.findByRole('heading', { name: 'Lead não encontrado' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Voltar para os leads' })).toHaveAttribute(
      'href',
      '/leads',
    )
  })

  it('endereço sem número nem chama a API', async () => {
    comSessao(atendente)

    renderizarApp('/leads/abc')

    expect(await screen.findByRole('heading', { name: 'Lead não encontrado' })).toBeInTheDocument()
  })
})
