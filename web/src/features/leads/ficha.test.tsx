import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import type { ItemLinhaDoTempo, LeadResponse } from '@/api/cliente'
import { paraCampoDataHora } from '@/lib/datas'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comCadastros, etapas, fichaLead, itemLinhaDoTempo } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

const comTelefoneNaoReconhecido = fichaLead({
  telefone: '(51) 9999-888',
  avisos: [
    { campo: 'telefone', mensagem: 'Telefone não reconhecido' },
    { campo: 'cpfCnpj', mensagem: 'Sem CPF/CNPJ' },
  ],
})

/** Ficha simulada, com a linha do tempo e os cadastros. Devolve os corpos das edições recebidas. */
function comFicha(lead: LeadResponse = comTelefoneNaoReconhecido, linha: ItemLinhaDoTempo[] = []) {
  const edicoes: Record<string, unknown>[] = []
  comCadastros()
  servidor.use(
    http.get('*/api/leads/:id', () => HttpResponse.json(lead)),
    http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json(linha)),
    http.put('*/api/leads/:id', async ({ request }) => {
      const corpo = (await request.json()) as Record<string, unknown>
      edicoes.push(corpo)
      return HttpResponse.json({ ...lead, ...corpo, versao: lead.versao + 1 })
    }),
  )
  return edicoes
}

/** Mudanças de etapa simuladas: devolve a ficha na etapa pedida, com a versão seguinte. */
function comMudancaDeEtapa(lead: LeadResponse = comTelefoneNaoReconhecido) {
  const pedidos: Record<string, unknown>[] = []
  servidor.use(
    http.post('*/api/leads/:id/etapa', async ({ request }) => {
      const corpo = (await request.json()) as { etapaId: number }
      pedidos.push(corpo)
      const etapa = etapas.find((e) => e.id === corpo.etapaId)!
      return HttpResponse.json({
        ...lead,
        etapa: { id: etapa.id, nome: etapa.nome, tipo: etapa.tipo },
        versao: lead.versao + 1,
      })
    }),
  )
  return pedidos
}

/** Interações simuladas. Devolve os corpos recebidos. */
function comRegistroDeInteracao() {
  const registros: Record<string, unknown>[] = []
  servidor.use(
    http.post('*/api/leads/:id/interacoes', async ({ request }) => {
      const corpo = (await request.json()) as Record<string, unknown>
      registros.push(corpo)
      return HttpResponse.json(
        itemLinhaDoTempo({ tipo: 'interacao', usuario: { id: atendente.id, nome: 'Adriana' } }),
        { status: 201 },
      )
    }),
  )
  return registros
}

/**
 * A seleção de etapa, depois que as etapas carregaram. A opção é procurada só dentro da seleção: na ficha inteira, cada
 * rodada da espera percorre a tela toda, e com a máquina ocupada a espera passava de 1 s.
 */
async function seletorDeEtapa() {
  const etapa = await screen.findByLabelText('Mudar para')
  await within(etapa).findByRole('option', { name: 'Perdido' })
  return etapa
}

// Os botões de contato são links de tel:, mailto: e wa.me, que o jsdom não sabe abrir.
const naoNavegar = (evento: MouseEvent) => {
  if (
    (evento.target as Element).closest('a[href^="tel:"], a[href^="mailto:"], a[href^="https:"]')
  ) {
    evento.preventDefault()
  }
}
beforeEach(() => document.addEventListener('click', naoNavegar))
afterEach(() => document.removeEventListener('click', naoNavegar))

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
    expect(screen.queryByLabelText('Passar para')).not.toBeInTheDocument()
  })

  it('o telefone do Brasil aparece no formato nacional, e salvar sem mexer nele funciona', async () => {
    comSessao(atendente)
    const edicoes = comFicha(fichaLead())
    const { usuario } = renderizarApp('/leads/1024')

    expect(await screen.findByLabelText('Telefone')).toHaveValue('(51) 99999-8888')
    expect(screen.getByRole('link', { name: 'WhatsApp' })).toHaveAttribute(
      'href',
      'https://wa.me/5551999998888',
    )
    await usuario.type(screen.getByLabelText('Empresa'), ' Ltda')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Dados salvos.')).toBeInTheDocument()
    // A API normaliza de volta para o mesmo E.164 e não conta o telefone como mudado.
    expect(edicoes).toEqual([
      expect.objectContaining({ telefone: '(51) 99999-8888', empresa: 'Empresa X Ltda' }),
    ])
  })

  it('o telefone de fora do Brasil aparece como está guardado', async () => {
    comSessao(atendente)
    comFicha(fichaLead({ telefone: '+16502530000' }))

    renderizarApp('/leads/1024')

    expect(await screen.findByLabelText('Telefone')).toHaveValue('+16502530000')
  })

  it('o admin passa o lead para outra pessoa ativa', async () => {
    comSessao(admin)
    const lead = fichaLead()
    comFicha(lead)
    const trocas: unknown[] = []
    servidor.use(
      http.post('*/api/leads/:id/responsavel', async ({ request }) => {
        trocas.push(await request.json())
        return HttpResponse.json({ ...lead, responsavel: { id: admin.id, nome: admin.nome } })
      }),
    )
    const { usuario } = renderizarApp('/leads/1024')

    const responsavel = await screen.findByLabelText('Passar para')
    await within(responsavel).findByRole('option', { name: admin.nome })
    expect(responsavel).toHaveValue(atendente.id)
    expect(within(responsavel).queryByRole('option', { name: 'Pietra' })).not.toBeInTheDocument()
    await usuario.selectOptions(responsavel, admin.nome)

    expect(await screen.findByText(`Lead passado para ${admin.nome}.`)).toBeInTheDocument()
    expect(trocas).toEqual([{ responsavelId: admin.id }])
    expect(responsavel).toHaveValue(admin.id)
  })

  it('o admin vê quando o lead está sem responsável', async () => {
    comSessao(admin)
    comFicha(fichaLead({ responsavel: null }))

    renderizarApp('/leads/1024')

    const responsavel = await screen.findByLabelText('Passar para')
    expect(within(responsavel).getByRole('option', { name: 'Sem responsável' })).toBeDisabled()
    expect(responsavel).toHaveValue('')
  })

  it('lead de teste não troca de responsável', async () => {
    comSessao(admin)
    comFicha(fichaLead({ responsavel: null, teste: true }))

    renderizarApp('/leads/1024')

    expect(await screen.findByText(/Lead de teste fica sem responsável/)).toBeInTheDocument()
    expect(screen.queryByLabelText('Passar para')).not.toBeInTheDocument()
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

describe('etapa, contato e interações na ficha', () => {
  const comEmail = fichaLead()

  it('o histórico mostra quem mudou e quando', async () => {
    comSessao(atendente)
    comFicha(comEmail, [
      itemLinhaDoTempo({
        tipo: 'etapa',
        ocorridoEm: '2026-10-06T12:30:00Z',
        usuario: { id: atendente.id, nome: 'Adriana' },
        etapa: {
          anterior: { id: 1, nome: 'Novo', tipo: 'aberta' },
          nova: { id: 2, nome: 'Em contato', tipo: 'aberta' },
          motivoPerda: null,
          motivoPerdaDetalhe: null,
        },
      }),
      itemLinhaDoTempo({
        tipo: 'entrada',
        entrada: {
          origem: 'meta_lead_ads',
          campanha: { id: 10, nome: 'Outubro | Serviço Y' },
          situacao: 'criado',
          teste: false,
        },
      }),
    ])

    renderizarApp('/leads/1024')

    const linha = within(await screen.findByRole('list', { name: 'Linha do tempo' }))
    const itens = linha.getAllByRole('listitem')
    expect(itens[0]).toHaveTextContent('Adriana moveu de Novo para Em contato')
    expect(itens[0]).toHaveTextContent('06/10/2026, 09:30')
    expect(itens[1]).toHaveTextContent(
      'Entrou por Meta (Facebook e Instagram) · Outubro | Serviço Y',
    )
    expect(screen.getByText('Ainda sem contato')).toBeInTheDocument()
  })

  it('muda a etapa sem perder o que foi digitado nos dados', async () => {
    comSessao(atendente)
    const edicoes = comFicha(comEmail)
    const mudancas = comMudancaDeEtapa(comEmail)
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.type(await screen.findByLabelText('Empresa'), ' Ltda')
    await usuario.selectOptions(await seletorDeEtapa(), 'Em contato')

    expect(await screen.findByText('Lead movido para Em contato.')).toBeInTheDocument()
    expect(mudancas).toEqual([{ etapaId: 2, motivoPerdaId: null, motivoPerdaDetalhe: null }])
    expect(screen.getByLabelText('Empresa')).toHaveValue('Empresa X Ltda')
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))
    expect(await screen.findByText('Dados salvos.')).toBeInTheDocument()
    expect(edicoes).toEqual([expect.objectContaining({ empresa: 'Empresa X Ltda', versao: 778 })])
  })

  it('Perdido pede o motivo, e cancelar deixa o lead onde estava', async () => {
    comSessao(atendente)
    comFicha(comEmail)
    const mudancas = comMudancaDeEtapa(comEmail)
    const { usuario } = renderizarApp('/leads/1024')

    const etapa = await seletorDeEtapa()
    await usuario.selectOptions(etapa, 'Perdido')
    const dialogo = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    expect(dialogo.queryByRole('option', { name: 'Motivo antigo' })).not.toBeInTheDocument()
    await usuario.click(dialogo.getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(etapa).toHaveValue('1')
    expect(mudancas).toEqual([])

    await usuario.selectOptions(etapa, 'Perdido')
    const outro = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    await usuario.click(outro.getByRole('button', { name: 'Marcar como perdido' }))
    expect(await outro.findByText('Escolha o motivo da perda.')).toBeInTheDocument()
    await usuario.selectOptions(outro.getByLabelText('Motivo'), 'Preço')
    await usuario.type(outro.getByLabelText('Detalhe (opcional)'), 'Achou caro')
    await usuario.click(outro.getByRole('button', { name: 'Marcar como perdido' }))

    expect(await screen.findByText('Lead movido para Perdido.')).toBeInTheDocument()
    expect(mudancas).toEqual([{ etapaId: 6, motivoPerdaId: 3, motivoPerdaDetalhe: 'Achou caro' }])
  })

  it('no lead anonimizado, Perdido pede só o motivo, sem o detalhe', async () => {
    const anonimizado = fichaLead({
      nome: 'Titular removido',
      email: null,
      telefone: null,
      empresa: null,
      anonimizadoEm: '2026-10-09T15:00:00Z',
      avisos: [],
    })
    comSessao(admin)
    comFicha(anonimizado)
    const mudancas = comMudancaDeEtapa(anonimizado)
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.selectOptions(await seletorDeEtapa(), 'Perdido')
    const dialogo = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    expect(dialogo.getByText(/a perda leva só o motivo, sem o detalhe/)).toBeInTheDocument()
    expect(dialogo.queryByLabelText('Detalhe (opcional)')).not.toBeInTheDocument()
    await usuario.selectOptions(dialogo.getByLabelText('Motivo'), 'Sem resposta')
    await usuario.click(dialogo.getByRole('button', { name: 'Marcar como perdido' }))

    expect(await screen.findByText('Lead movido para Perdido.')).toBeInTheDocument()
    expect(mudancas).toEqual([{ etapaId: 6, motivoPerdaId: 1, motivoPerdaDetalhe: null }])
  })

  it('Ganho recusado mostra a mensagem da API e o lead fica onde estava', async () => {
    comSessao(atendente)
    comFicha(comEmail)
    servidor.use(
      http.post('*/api/leads/:id/etapa', () =>
        problema(400, {
          detail: 'Para marcar como Ganho, informe um CPF ou CNPJ válido na ficha do lead.',
        }),
      ),
    )
    const { usuario } = renderizarApp('/leads/1024')

    const etapa = await seletorDeEtapa()
    await usuario.selectOptions(etapa, 'Ganho')

    expect(
      await screen.findByText(
        'Para marcar como Ganho, informe um CPF ou CNPJ válido na ficha do lead.',
      ),
    ).toBeInTheDocument()
    expect(etapa).toHaveValue('1')
  })

  it('os botões de contato abrem o registro com o tipo já escolhido', async () => {
    comSessao(atendente)
    comFicha(comEmail)
    const registros = comRegistroDeInteracao()
    const { usuario } = renderizarApp('/leads/1024')

    const whatsapp = await screen.findByRole('link', { name: 'WhatsApp' })
    expect(whatsapp).toHaveAttribute('href', 'https://wa.me/5551999998888')
    expect(screen.getByRole('link', { name: 'Ligar' })).toHaveAttribute(
      'href',
      'tel:+5551999998888',
    )
    expect(screen.getByRole('link', { name: 'E-mail' })).toHaveAttribute(
      'href',
      'mailto:maria@empresax.com.br',
    )
    await usuario.click(whatsapp)
    const dialogo = within(await screen.findByRole('dialog', { name: 'Registrar interação' }))
    expect(dialogo.getByLabelText('Tipo')).toHaveValue('whatsapp')
    await usuario.type(dialogo.getByLabelText('Descrição'), 'Mandei o orçamento')
    await usuario.click(dialogo.getByRole('button', { name: 'Registrar' }))

    expect(await screen.findByText('Interação registrada.')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(registros).toHaveLength(1)
    expect(registros[0]).toMatchObject({ tipo: 'whatsapp', descricao: 'Mandei o orçamento' })
    // Sem mudar o campo (que só tem minutos), vai a hora do registro, com os segundos.
    const ocorridoEm = new Date(registros[0].ocorridoEm as string)
    expect(ocorridoEm.getTime()).toBeLessThanOrEqual(Date.now())
    expect(Date.now() - ocorridoEm.getTime()).toBeLessThan(5_000)
  })

  it('uma data passada escolhida no campo vai como foi escolhida', async () => {
    comSessao(atendente)
    comFicha(comEmail)
    const registros = comRegistroDeInteracao()
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.click(await screen.findByRole('link', { name: 'Ligar' }))
    const dialogo = within(await screen.findByRole('dialog', { name: 'Registrar interação' }))
    fireEvent.change(dialogo.getByLabelText('Quando'), { target: { value: '2026-09-29T10:15' } })
    await usuario.click(dialogo.getByRole('button', { name: 'Registrar' }))

    expect(await screen.findByText('Interação registrada.')).toBeInTheDocument()
    expect(registros).toEqual([
      {
        tipo: 'ligacao',
        descricao: null,
        ocorridoEm: new Date('2026-09-29T10:15').toISOString(),
      },
    ])
  })

  it('telefone com aviso de formato não vira botão de WhatsApp nem de ligação', async () => {
    comSessao(atendente)
    comFicha()

    renderizarApp('/leads/1024')

    expect(await screen.findByRole('link', { name: 'E-mail' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'WhatsApp' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ligar' })).not.toBeInTheDocument()
  })

  it('a nota exige texto e a data não pode estar no futuro', async () => {
    comSessao(atendente)
    comFicha(comEmail)
    const registros = comRegistroDeInteracao()
    const { usuario } = renderizarApp('/leads/1024')

    await usuario.click(await screen.findByRole('button', { name: 'Registrar interação' }))
    const dialogo = within(await screen.findByRole('dialog', { name: 'Registrar interação' }))
    expect(dialogo.getByLabelText('Tipo')).toHaveValue('nota')
    fireEvent.change(dialogo.getByLabelText('Quando'), {
      target: { value: paraCampoDataHora(new Date(Date.now() + 2 * 86_400_000)) },
    })
    await usuario.click(dialogo.getByRole('button', { name: 'Registrar' }))

    expect(await dialogo.findByText('Escreva a nota.')).toBeInTheDocument()
    expect(dialogo.getByText('A data não pode estar no futuro.')).toBeInTheDocument()
    expect(registros).toEqual([])
  })
})

/**
 * Ficha simulada que outra pessoa pode mudar durante o teste (`mudar`). A edição só vale com a versão atual; com outra,
 * responde o conflito. Devolve os corpos das edições recebidas.
 */
function comFichaQueMuda() {
  let atual = fichaLead()
  const edicoes: Record<string, unknown>[] = []
  comCadastros()
  servidor.use(
    http.get('*/api/leads/:id', () => HttpResponse.json(atual)),
    http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
    http.put('*/api/leads/:id', async ({ request }) => {
      const corpo = (await request.json()) as Record<string, unknown> & { versao: number }
      edicoes.push(corpo)
      if (corpo.versao !== atual.versao) {
        return problema(409, {
          detail:
            'O lead foi alterado por outra pessoa. Recarregue a página para ver a versão atual.',
        })
      }
      atual = { ...atual, ...corpo, versao: atual.versao + 1 }
      return HttpResponse.json(atual)
    }),
  )
  const mudar = (parcial: Partial<LeadResponse>) => {
    atual = { ...atual, ...parcial, versao: atual.versao + 1 }
  }
  return { edicoes, mudar }
}

describe('ficha com o tempo real', () => {
  /** A pessoa digita na empresa, e outra pessoa muda o lead; o hub avisa. */
  async function editarEnquantoOutraPessoaMuda(parcial: Partial<LeadResponse>) {
    comSessao(atendente)
    const ficha = comFichaQueMuda()
    const app = renderizarApp('/leads/1024')
    await app.usuario.type(await screen.findByLabelText('Empresa'), ' Ltda')
    await waitFor(() => expect(app.hub.estado.conectada).toBe(true))
    ficha.mudar(parcial)
    app.hub.emitir('LeadsAlterados', [1024])
    return { ...app, ...ficha }
  }

  it('o que a pessoa digitou não some quando outra pessoa muda os dados, e salvar responde o conflito', async () => {
    const { usuario, edicoes } = await editarEnquantoOutraPessoaMuda({ nome: 'Maria Souza Lima' })

    expect(await screen.findByText('Outra pessoa alterou os dados deste lead.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Maria Souza Lima' })).toBeInTheDocument()
    expect(screen.getByLabelText('Empresa')).toHaveValue('Empresa X Ltda')
    expect(screen.getByLabelText('Nome')).toHaveValue('Maria Souza')

    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText(/alterado por outra pessoa\. Recarregue/)).toBeInTheDocument()
    expect(edicoes).toEqual([expect.objectContaining({ empresa: 'Empresa X Ltda', versao: 777 })])
  })

  it('"Ver a versão atual" troca o que foi digitado pelos dados novos', async () => {
    const { usuario } = await editarEnquantoOutraPessoaMuda({ nome: 'Maria Souza Lima' })

    await usuario.click(await screen.findByRole('button', { name: 'Ver a versão atual' }))

    expect(await screen.findByLabelText('Nome')).toHaveValue('Maria Souza Lima')
    expect(screen.getByLabelText('Empresa')).toHaveValue('Empresa X')
    expect(screen.queryByText('Outra pessoa alterou os dados deste lead.')).not.toBeInTheDocument()
  })

  it('se outra pessoa só mudou a etapa, o que foi digitado é salvo com a versão nova', async () => {
    const { usuario, edicoes } = await editarEnquantoOutraPessoaMuda({
      etapa: { id: 2, nome: 'Em contato', tipo: 'aberta' },
    })

    expect(await screen.findAllByText('Em contato', { selector: 'span' })).not.toHaveLength(0)
    expect(screen.queryByText('Outra pessoa alterou os dados deste lead.')).not.toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Dados salvos.')).toBeInTheDocument()
    expect(edicoes).toEqual([expect.objectContaining({ empresa: 'Empresa X Ltda', versao: 778 })])
  })
})
