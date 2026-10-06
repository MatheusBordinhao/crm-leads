import { screen, waitFor, within } from '@testing-library/react'
import type { UserEvent } from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { MudarEtapaRequest, QuadroFunilResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { cartao, comQuadro, fichaLead, quadroFunil } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

afterEach(() => vi.restoreAllMocks())

/**
 * O jsdom não calcula layout. Cada área do quadro (as colunas, Ganho e Perdido, marcadas com
 * `data-area-funil`) ganha uma posição, lado a lado, e o que está dentro dela fica no topo da área.
 */
function posicionarAreas() {
  const retangulo = (x: number, y: number, largura: number, altura: number) =>
    ({
      x,
      y,
      left: x,
      top: y,
      right: x + largura,
      bottom: y + altura,
      width: largura,
      height: altura,
      toJSON: () => ({}),
    }) as DOMRect
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (
    this: HTMLElement,
  ) {
    const area = this.closest<HTMLElement>('[data-area-funil]')
    if (!area) {
      return retangulo(0, 0, 0, 0)
    }
    const posicao = [...document.querySelectorAll('[data-area-funil]')].indexOf(area) * 300
    return this === area ? retangulo(posicao, 0, 280, 600) : retangulo(posicao + 10, 50, 260, 80)
  })
}

/**
 * Servidor com o estado do quadro: a mudança de etapa leva o lead para a etapa pedida, e o quadro
 * recarregado já mostra o lead lá. Devolve os pedidos de mudança.
 */
function comServidorDoQuadro() {
  let quadro: QuadroFunilResponse = quadroFunil()
  const mudancas: (MudarEtapaRequest & { leadId: number })[] = []
  comQuadro(quadro)
  servidor.use(
    http.get('*/api/funil', () => HttpResponse.json(quadro)),
    http.post('*/api/leads/:id/etapa', async ({ params, request }) => {
      const leadId = Number(params.id)
      const dados = (await request.json()) as MudarEtapaRequest
      mudancas.push({ leadId, ...dados })
      const lead = quadro.colunas.flatMap((c) => c.leads).find((l) => l.id === leadId)!
      const colunas = quadro.colunas.map((c) => {
        const leads = c.leads.filter((l) => l.id !== leadId)
        return {
          ...c,
          leads: c.etapa.id === dados.etapaId ? [lead, ...leads] : leads,
          total: c.etapa.id === dados.etapaId ? leads.length + 1 : leads.length,
        }
      })
      const somar = (area: QuadroFunilResponse['ganho']) =>
        area.etapa.id === dados.etapaId ? { ...area, periodo: area.periodo + 1 } : area
      quadro = { ...quadro, colunas, ganho: somar(quadro.ganho), perdido: somar(quadro.perdido) }
      const etapa = [...colunas.map((c) => c.etapa), quadro.ganho.etapa, quadro.perdido.etapa].find(
        (e) => e.id === dados.etapaId,
      )!
      return HttpResponse.json({ id: leadId, nome: lead.nome, etapa, versao: 2 })
    }),
  )
  return mudancas
}

function noQuadro() {
  // Com o diálogo aberto, o Radix esconde o resto da página (aria-hidden); o quadro continua lá.
  return within(screen.getByRole('region', { name: 'Quadro do funil', hidden: true }))
}

function coluna(nome: string) {
  return within(noQuadro().getByRole('group', { name: nome, hidden: true }))
}

/** Arrasta pelo teclado: pega o cartão pela alça, anda `etapas` áreas para a direita e solta. */
async function arrastar(usuario: UserEvent, nome: string, etapas: number) {
  noQuadro()
    .getByRole('button', { name: `Mover ${nome}` })
    .focus()
  await usuario.keyboard('[Space]')
  for (let passo = 0; passo < etapas; passo++) {
    await usuario.keyboard('[ArrowRight]')
  }
  await usuario.keyboard('[Space]')
}

describe('quadro do funil', () => {
  it('a tela inicial é o funil', async () => {
    comSessao(atendente)
    comQuadro()

    const { router } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/funil')
  })

  it('mostra as colunas abertas, os cartões e os números de Ganho e Perdido', async () => {
    comSessao(atendente)
    comQuadro()

    renderizarApp('/funil')

    await screen.findByRole('region', { name: 'Quadro do funil' })
    const novo = coluna('Novo')
    expect(novo.getByRole('link', { name: 'Maria Souza' })).toHaveAttribute('href', '/leads/1024')
    expect(novo.getByText('Empresa X')).toBeInTheDocument()
    expect(novo.getByText('Meta (Facebook e Instagram)')).toBeInTheDocument()
    expect(novo.queryByText('Adriana')).not.toBeInTheDocument()
    expect(coluna('Em contato').getByText('João Lima')).toBeInTheDocument()
    expect(coluna('Qualificado').queryAllByRole('listitem')).toHaveLength(0)
    const ganho = noQuadro().getByRole('group', { name: 'Ganho' })
    expect(ganho).toHaveTextContent('3 leads nos últimos 30 dias')
    expect(within(ganho).getByRole('link', { name: 'Ver na lista' })).toHaveAttribute(
      'href',
      '/leads?etapa=5',
    )
    expect(noQuadro().getByRole('group', { name: 'Perdido' })).toHaveTextContent(
      '1 lead nos últimos 30 dias',
    )
  })

  it('o admin vê a responsável no cartão', async () => {
    comSessao(admin)
    comQuadro()

    renderizarApp('/funil')

    await screen.findByRole('region', { name: 'Quadro do funil' })
    expect(coluna('Novo').getByText('Adriana')).toBeInTheDocument()
  })

  it('arrastar para Perdido abre o pedido de motivo, e cancelar devolve o cartão', async () => {
    comSessao(atendente)
    const mudancas = comServidorDoQuadro()
    posicionarAreas()
    const { usuario } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })

    // Novo → Em contato → Qualificado → Proposta enviada → Ganho → Perdido
    await arrastar(usuario, 'Maria Souza', 5)

    const dialogo = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    expect(coluna('Novo').queryByText('Maria Souza')).not.toBeInTheDocument()
    await usuario.click(dialogo.getByRole('button', { name: 'Cancelar' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(coluna('Novo').getByText('Maria Souza')).toBeInTheDocument()
    expect(mudancas).toEqual([])
  })

  it('confirmar o motivo leva o lead para Perdido', async () => {
    comSessao(atendente)
    const mudancas = comServidorDoQuadro()
    posicionarAreas()
    const { usuario } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })

    await arrastar(usuario, 'Maria Souza', 5)
    const dialogo = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    await usuario.selectOptions(dialogo.getByLabelText('Motivo'), 'Sem resposta')
    await usuario.click(dialogo.getByRole('button', { name: 'Marcar como perdido' }))

    expect(await noQuadro().findByText('2 leads nos últimos 30 dias')).toBeInTheDocument()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(coluna('Novo').queryByText('Maria Souza')).not.toBeInTheDocument()
    expect(mudancas).toEqual([
      { leadId: 1024, etapaId: 6, motivoPerdaId: 1, motivoPerdaDetalhe: null },
    ])
  })

  it('arrastar para outra coluna move o cartão', async () => {
    comSessao(atendente)
    const mudancas = comServidorDoQuadro()
    posicionarAreas()
    const { usuario } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })

    await arrastar(usuario, 'Maria Souza', 1)

    expect(await coluna('Em contato').findByText('Maria Souza')).toBeInTheDocument()
    expect(coluna('Novo').queryByText('Maria Souza')).not.toBeInTheDocument()
    expect(mudancas).toEqual([
      { leadId: 1024, etapaId: 2, motivoPerdaId: null, motivoPerdaDetalhe: null },
    ])
  })

  it('Ganho recusado devolve o cartão e mostra a mensagem', async () => {
    comSessao(atendente)
    comServidorDoQuadro()
    servidor.use(
      http.post('*/api/leads/:id/etapa', () =>
        problema(400, {
          detail: 'Para marcar como Ganho, informe um CPF ou CNPJ válido na ficha do lead.',
        }),
      ),
    )
    posicionarAreas()
    const { usuario } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })

    await arrastar(usuario, 'Maria Souza', 4)

    expect(
      await screen.findByText(
        'Para marcar como Ganho, informe um CPF ou CNPJ válido na ficha do lead.',
      ),
    ).toBeInTheDocument()
    expect(await coluna('Novo').findByText('Maria Souza')).toBeInTheDocument()
    expect(noQuadro().getByRole('group', { name: 'Ganho' })).toHaveTextContent(
      '3 leads nos últimos 30 dias',
    )
    expect(screen.getByRole('button', { name: 'Abrir a ficha' })).toBeInTheDocument()
  })

  it('no celular, a lista por etapa move sem arrastar', async () => {
    comSessao(atendente)
    const mudancas = comServidorDoQuadro()
    const { usuario } = renderizarApp('/funil')

    const lista = within(await screen.findByRole('region', { name: 'Leads por etapa' }))
    expect(lista.getByRole('list', { name: 'Leads em Novo' })).toHaveTextContent('Maria Souza')
    await usuario.selectOptions(lista.getByLabelText('Etapa'), 'Em contato (1)')
    expect(lista.getByRole('list', { name: 'Leads em Em contato' })).toHaveTextContent('João Lima')
    await usuario.selectOptions(lista.getByLabelText('Etapa'), 'Novo (1)')

    await usuario.selectOptions(lista.getByLabelText('Mover Maria Souza para'), 'Perdido')
    const dialogo = within(await screen.findByRole('dialog', { name: 'Motivo da perda' }))
    await usuario.click(dialogo.getByRole('button', { name: 'Cancelar' }))
    expect(lista.getByRole('list', { name: 'Leads em Novo' })).toHaveTextContent('Maria Souza')
    expect(mudancas).toEqual([])

    await usuario.selectOptions(lista.getByLabelText('Mover Maria Souza para'), 'Qualificado')

    expect(await lista.findByText('Nenhum lead nesta etapa.')).toBeInTheDocument()
    expect(mudancas).toEqual([
      { leadId: 1024, etapaId: 3, motivoPerdaId: null, motivoPerdaDetalhe: null },
    ])
  })

  it('os filtros vão para a API e ficam no endereço', async () => {
    comSessao(admin)
    const pedidos = comQuadro()
    const { usuario, router } = renderizarApp('/funil')

    await usuario.type(await screen.findByLabelText('Buscar no funil'), 'Maria')
    await usuario.click(screen.getByRole('button', { name: 'Buscar' }))
    await usuario.selectOptions(screen.getByLabelText('Origem'), 'Google Ads')
    await usuario.selectOptions(screen.getByLabelText('Responsável'), 'Sem responsável')

    await vi.waitFor(() => {
      const ultimo = pedidos.at(-1)!
      expect(ultimo.searchParams.get('busca')).toBe('Maria')
      expect(ultimo.searchParams.get('origem')).toBe('google_ads')
      expect(ultimo.searchParams.get('semResponsavel')).toBe('true')
    })
    expect(router.state.location.search).toBe('?busca=Maria&origem=google_ads&responsavel=sem')
  })

  it('a atendente não tem o filtro de responsável', async () => {
    comSessao(atendente)
    comQuadro()

    renderizarApp('/funil')

    expect(await screen.findByLabelText('Origem')).toBeInTheDocument()
    expect(screen.queryByLabelText('Responsável')).not.toBeInTheDocument()
  })

  it('um lead novo aparece no quadro sem recarregar a página', async () => {
    comSessao(atendente)
    let quadro = quadroFunil()
    comQuadro(quadro)
    servidor.use(http.get('*/api/funil', () => HttpResponse.json(quadro)))
    const { hub } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })
    expect(coluna('Novo').getByText('Maria Souza')).toBeInTheDocument()
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    quadro = comLeadNovoEmNovo(quadro)
    hub.emitir('LeadsAlterados', [2048])

    expect(await coluna('Novo').findByText('Lead Novo')).toBeInTheDocument()
  })

  it('durante o arraste, o quadro fica como estava e se atualiza ao cancelar', async () => {
    comSessao(atendente)
    let quadro = quadroFunil()
    comQuadro(quadro)
    servidor.use(http.get('*/api/funil', () => HttpResponse.json(quadro)))
    posicionarAreas()
    const { hub, usuario, queryClient } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })
    await waitFor(() => expect(hub.estado.conectada).toBe(true))

    noQuadro().getByRole('button', { name: 'Mover Maria Souza' }).focus()
    await usuario.keyboard('[Space]')
    quadro = comLeadNovoEmNovo(quadro)
    hub.emitir('LeadsAlterados', [2048])

    // O quadro novo já chegou da API, mas a tela continua com o do começo do arraste.
    await waitFor(() =>
      expect(JSON.stringify(queryClient.getQueriesData({ queryKey: ['funil'] }))).toContain(
        'Lead Novo',
      ),
    )
    expect(coluna('Novo').queryByText('Lead Novo')).not.toBeInTheDocument()

    await usuario.keyboard('[Escape]')

    expect(await coluna('Novo').findByText('Lead Novo')).toBeInTheDocument()
  })

  it('não recarrega o quadro no meio de uma mudança de etapa, só depois dela', async () => {
    comSessao(atendente)
    const quadro = quadroFunil()
    const consultas: URL[] = []
    let responder = () => {}
    const resposta = new Promise<void>((resolver) => {
      responder = resolver
    })
    comQuadro(quadro)
    servidor.use(
      http.get('*/api/funil', ({ request }) => {
        consultas.push(new URL(request.url))
        return HttpResponse.json(quadro)
      }),
      http.post('*/api/leads/:id/etapa', async () => {
        await resposta
        return HttpResponse.json(
          fichaLead({ etapa: { id: 2, nome: 'Em contato', tipo: 'aberta' }, versao: 2 }),
        )
      }),
    )
    const { hub, usuario } = renderizarApp('/funil')
    await screen.findByRole('region', { name: 'Quadro do funil' })
    await waitFor(() => expect(hub.estado.conectada).toBe(true))
    const antes = consultas.length

    await usuario.selectOptions(screen.getByLabelText('Mover Maria Souza para'), 'Em contato')
    hub.emitir('LeadsAlterados', [1025])
    await new Promise((esperar) => setTimeout(esperar, 100))

    expect(consultas).toHaveLength(antes)
    responder()
    await waitFor(() => expect(consultas.length).toBeGreaterThan(antes))
  })
})

/** O quadro com um lead novo, o Lead Novo, no topo da coluna Novo. */
function comLeadNovoEmNovo(quadro: QuadroFunilResponse): QuadroFunilResponse {
  const novo = cartao({ id: 2048, nome: 'Lead Novo', criadoEm: '2026-10-06T12:00:00Z' })
  return {
    ...quadro,
    colunas: quadro.colunas.map((c, i) =>
      i === 0 ? { ...c, total: c.total + 1, leads: [novo, ...c.leads] } : c,
    ),
  }
}
