import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import type { ConfiguracoesResponse, EtapaResponse, MotivoPerdaResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comQuadro, etapas, motivos } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

type Pedido = { metodo: string; caminho: string; corpo: unknown }

/** API simulada com estado: as mudanças aparecem na lista recarregada. Devolve os pedidos de escrita. */
function comCadastrosDoFunil() {
  let listaEtapas: EtapaResponse[] = etapas.map((etapa) => ({ ...etapa }))
  let listaMotivos: MotivoPerdaResponse[] = motivos.map((motivo) => ({ ...motivo }))
  let configuracoes: ConfiguracoesResponse = {
    exigirDocumentoParaGanho: true,
    distribuicaoAutomatica: true,
  }
  const pedidos: Pedido[] = []
  const registrar = async (request: Request) => {
    const texto = await request.text()
    const corpo: unknown = texto ? JSON.parse(texto) : null
    pedidos.push({ metodo: request.method, caminho: new URL(request.url).pathname, corpo })
    return corpo as Record<string, unknown> | null
  }
  const etapa = (id: unknown) => listaEtapas.find((e) => e.id === Number(id))!

  comSessao(admin)
  servidor.use(
    http.get('*/api/etapas', () => HttpResponse.json(listaEtapas)),
    http.get('*/api/motivos-perda', () => HttpResponse.json(listaMotivos)),
    http.get('*/api/configuracoes', () => HttpResponse.json(configuracoes)),
    http.post('*/api/etapas', async ({ request }) => {
      const { nome } = (await registrar(request)) as { nome: string }
      if (listaEtapas.some((e) => e.nome.toLowerCase() === nome.toLowerCase())) {
        return problema(400, {
          errors: {
            nome: ['Já existe uma etapa com esse nome. Se ela estiver desativada, reative-a.'],
          },
        })
      }
      const nova: EtapaResponse = { id: 20, nome, ordem: 5, tipo: 'aberta', ativa: true }
      listaEtapas = [
        ...listaEtapas.filter((e) => e.tipo === 'aberta'),
        nova,
        ...listaEtapas.filter((e) => e.tipo !== 'aberta'),
      ]
      return HttpResponse.json(nova, { status: 201 })
    }),
    // Antes da rota com o id: "ordem" também casaria com ":id".
    http.put('*/api/etapas/ordem', async ({ request }) => {
      const { ids } = (await registrar(request)) as { ids: number[] }
      listaEtapas = [...ids.map(etapa), ...listaEtapas.filter((e) => e.tipo !== 'aberta')]
      return HttpResponse.json(listaEtapas)
    }),
    http.put('*/api/etapas/:id', async ({ request, params }) => {
      const { nome } = (await registrar(request)) as { nome: string }
      etapa(params.id).nome = nome
      return HttpResponse.json(etapa(params.id))
    }),
    http.post('*/api/etapas/:id/desativar', async ({ request, params }) => {
      await registrar(request)
      etapa(params.id).ativa = false
      return HttpResponse.json(etapa(params.id))
    }),
    http.post('*/api/etapas/:id/reativar', async ({ request, params }) => {
      await registrar(request)
      etapa(params.id).ativa = true
      return HttpResponse.json(etapa(params.id))
    }),
    http.post('*/api/motivos-perda', async ({ request }) => {
      const { nome } = (await registrar(request)) as { nome: string }
      const novo = { id: 30, nome, ativo: true }
      listaMotivos = [...listaMotivos, novo]
      return HttpResponse.json(novo, { status: 201 })
    }),
    http.put('*/api/motivos-perda/:id', async ({ request, params }) => {
      const dados = (await registrar(request)) as { nome: string; ativo: boolean }
      listaMotivos = listaMotivos.map((m) => (m.id === Number(params.id) ? { ...m, ...dados } : m))
      return HttpResponse.json(listaMotivos.find((m) => m.id === Number(params.id)))
    }),
    http.put('*/api/configuracoes', async ({ request }) => {
      configuracoes = (await registrar(request)) as ConfiguracoesResponse
      return HttpResponse.json(configuracoes)
    }),
  )
  return pedidos
}

async function listaDeEtapas() {
  return within(await screen.findByRole('list', { name: 'Etapas abertas' }))
}

const nomesDos = (lista: HTMLElement[]) => lista.map((item) => item.textContent)

describe('configuração do funil', () => {
  it('a atendente não entra e volta ao funil', async () => {
    comSessao(atendente)
    comQuadro()

    const { router } = renderizarApp('/configuracoes/funil')

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/funil')
  })

  it('o admin chega pelo menu e vê as etapas na ordem, Ganho e Perdido no fim e os motivos', async () => {
    comCadastrosDoFunil()
    comQuadro()
    const { usuario } = renderizarApp('/funil')

    const menu = await screen.findByRole('navigation', { name: 'Menu principal' })
    await usuario.click(within(menu).getByRole('link', { name: 'Etapas e motivos' }))

    const abertas = await listaDeEtapas()
    expect(nomesDos(abertas.getAllByRole('listitem'))).toEqual([
      expect.stringContaining('Novo'),
      expect.stringContaining('Em contato'),
      expect.stringContaining('Qualificado'),
      expect.stringContaining('Proposta enviada'),
    ])
    const fechamento = within(screen.getByRole('list', { name: 'Etapas de fechamento' }))
    expect(fechamento.getByText('Ganho')).toBeInTheDocument()
    expect(fechamento.getByText('Perdido')).toBeInTheDocument()
    expect(fechamento.queryByRole('button', { name: /Desativar/ })).not.toBeInTheDocument()
    expect(screen.getByRole('switch', { name: 'Ativo: Motivo antigo' })).not.toBeChecked()
    expect(screen.getByRole('switch', { name: 'Ativo: Preço' })).toBeChecked()
  })

  it('cria uma etapa e mostra o erro de nome repetido', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    const campo = await screen.findByLabelText('Nova etapa')
    await usuario.type(campo, 'Negociação')
    await usuario.click(within(campo.closest('form')!).getByRole('button', { name: 'Adicionar' }))

    expect(await (await listaDeEtapas()).findByText('Negociação')).toBeInTheDocument()
    expect(campo).toHaveValue('')
    await usuario.type(campo, 'novo')
    await usuario.click(within(campo.closest('form')!).getByRole('button', { name: 'Adicionar' }))
    expect(
      await screen.findByText(
        'Já existe uma etapa com esse nome. Se ela estiver desativada, reative-a.',
      ),
    ).toBeInTheDocument()
    expect(pedidos.map((p) => p.corpo)).toEqual([{ nome: 'Negociação' }, { nome: 'novo' }])
  })

  it('muda a ordem com as setas', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    const abertas = await listaDeEtapas()
    expect(abertas.getByRole('button', { name: 'Subir Novo' })).toBeDisabled()
    await usuario.click(abertas.getByRole('button', { name: 'Descer Novo' }))

    await waitFor(() =>
      expect(
        nomesDos(
          within(screen.getByRole('list', { name: 'Etapas abertas' })).getAllByRole('listitem'),
        )[0],
      ).toContain('Em contato'),
    )
    expect(pedidos).toEqual([
      { metodo: 'PUT', caminho: '/api/etapas/ordem', corpo: { ids: [2, 1, 3, 4] } },
    ])
  })

  it('desativar pede para onde vão os leads, e a etapa pode voltar', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    await usuario.click(
      (await listaDeEtapas()).getByRole('button', { name: 'Desativar Qualificado' }),
    )
    const dialogo = within(await screen.findByRole('dialog', { name: 'Desativar Qualificado?' }))
    expect(dialogo.getByLabelText('Mover os leads para')).toHaveValue('1')
    await usuario.selectOptions(dialogo.getByLabelText('Mover os leads para'), 'Em contato')
    await usuario.click(dialogo.getByRole('button', { name: 'Desativar' }))

    expect(await screen.findByText('Etapa Qualificado desativada.')).toBeInTheDocument()
    const reativar = await (
      await listaDeEtapas()
    ).findByRole('button', { name: 'Reativar Qualificado' })
    await usuario.click(reativar)

    expect(
      await (await listaDeEtapas()).findByRole('button', { name: 'Desativar Qualificado' }),
    ).toBeInTheDocument()
    expect(pedidos).toEqual([
      { metodo: 'POST', caminho: '/api/etapas/3/desativar', corpo: { moverLeadsParaId: 2 } },
      { metodo: 'POST', caminho: '/api/etapas/3/reativar', corpo: null },
    ])
  })

  it('renomeia Ganho', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    await usuario.click(await screen.findByRole('button', { name: 'Renomear Ganho' }))
    const dialogo = within(await screen.findByRole('dialog', { name: 'Renomear Ganho' }))
    await usuario.clear(dialogo.getByLabelText('Nome'))
    await usuario.type(dialogo.getByLabelText('Nome'), 'Fechado')
    await usuario.click(dialogo.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Fechado')).toBeInTheDocument()
    expect(pedidos).toEqual([
      { metodo: 'PUT', caminho: '/api/etapas/5', corpo: { nome: 'Fechado' } },
    ])
  })

  it('desativa um motivo pelo interruptor e cria outro', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    await usuario.click(await screen.findByRole('switch', { name: 'Ativo: Preço' }))
    expect(await screen.findByText('Preço saiu da lista.')).toBeInTheDocument()
    await usuario.type(screen.getByLabelText('Novo motivo'), 'Mudou de cidade')
    await usuario.click(
      within(screen.getByLabelText('Novo motivo').closest('form')!).getByRole('button', {
        name: 'Adicionar',
      }),
    )

    expect(await screen.findByRole('switch', { name: 'Ativo: Mudou de cidade' })).toBeChecked()
    expect(pedidos).toEqual([
      { metodo: 'PUT', caminho: '/api/motivos-perda/3', corpo: { nome: 'Preço', ativo: false } },
      { metodo: 'POST', caminho: '/api/motivos-perda', corpo: { nome: 'Mudou de cidade' } },
    ])
  })

  it('desliga a exigência de CPF/CNPJ para Ganho', async () => {
    const pedidos = comCadastrosDoFunil()
    const { usuario } = renderizarApp('/configuracoes/funil')

    const interruptor = await screen.findByRole('switch', {
      name: 'Exigir CPF/CNPJ válido para marcar Ganho',
    })
    await vi.waitFor(() => expect(interruptor).toBeEnabled())
    expect(interruptor).toBeChecked()
    await usuario.click(interruptor)

    expect(await screen.findByText('Ganho não exige mais CPF/CNPJ.')).toBeInTheDocument()
    expect(interruptor).not.toBeChecked()
    expect(pedidos).toEqual([
      {
        metodo: 'PUT',
        caminho: '/api/configuracoes',
        corpo: { exigirDocumentoParaGanho: false, distribuicaoAutomatica: true },
      },
    ])
  })
})
