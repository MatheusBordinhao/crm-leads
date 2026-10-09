import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { admin, atendente, comSessao } from '@/test/api'
import { comAuditoria, registro } from '@/test/auditoria'
import { comLista, comQuadro } from '@/test/leads'
import { comPainel } from '@/test/painel'
import { renderizarApp } from '@/test/renderizar'

function ultimoPedido(pedidos: URL[]) {
  return Object.fromEntries(pedidos.at(-1)!.searchParams)
}

async function tabela() {
  return within(await screen.findByRole('table', { name: 'Registros da auditoria' }))
}

describe('auditoria', () => {
  it('mostra quando, quem, a ação, o registro afetado, os detalhes e o IP', async () => {
    comSessao(admin)
    comAuditoria([
      registro(),
      registro({
        id: 2,
        ocorridoEm: '2026-10-09T13:30:00Z',
        usuario: null,
        acao: 'login.falha',
        entidade: null,
        entidadeId: null,
        detalhes: { motivo: 'usuario_desconhecido' },
        ip: '10.0.0.1',
      }),
    ])
    comPainel()

    const { usuario } = renderizarApp('/')
    await usuario.click(await screen.findByRole('link', { name: 'Auditoria' }))

    expect(await screen.findByRole('heading', { name: 'Auditoria' })).toBeInTheDocument()
    const linhas = (await tabela()).getAllByRole('row').slice(1)
    expect(
      linhas.map((linha) =>
        within(linha)
          .getAllByRole('cell')
          .map((celula) => celula.textContent),
      ),
    ).toEqual([
      [
        '09/10/2026, 12:00',
        'Matheus',
        'Lead anonimizado',
        'Lead 1024',
        '1 entrada e 2 interações limpas; 3 avisos ao n8n apagados',
        '177.10.20.30',
      ],
      [
        '09/10/2026, 10:30',
        'Sem usuário',
        'Login recusado',
        '—',
        'E-mail desconhecido',
        '10.0.0.1',
      ],
    ])
    expect(within(linhas[0]).getByRole('link', { name: 'Lead 1024' })).toHaveAttribute(
      'href',
      '/leads/1024',
    )
    expect(screen.getByText('2 registros')).toBeInTheDocument()
  })

  it('no celular, cada registro é um cartão', async () => {
    comSessao(admin)
    comAuditoria()

    renderizarApp('/auditoria')

    const cartoes = await screen.findByRole('list', { name: 'Registros da auditoria' })
    const cartao = within(within(cartoes).getByRole('listitem'))
    expect(cartao.getByText('Lead anonimizado')).toBeInTheDocument()
    expect(cartao.getByRole('link', { name: 'Lead 1024' })).toHaveAttribute('href', '/leads/1024')
    expect(cartao.getByText('IP 177.10.20.30')).toBeInTheDocument()
  })

  it('os filtros vão para a API e ficam no endereço', async () => {
    comSessao(admin)
    const pedidos = comAuditoria()
    const { usuario, router } = renderizarApp('/auditoria')

    await tabela()
    await screen.findByRole('option', { name: 'Matheus' })
    await usuario.selectOptions(screen.getByLabelText('Ação'), 'Lead anonimizado')
    await usuario.selectOptions(screen.getByLabelText('Quem fez'), 'Matheus')
    fireEvent.change(screen.getByLabelText('A partir de'), { target: { value: '2026-10-01' } })

    await waitFor(() =>
      expect(ultimoPedido(pedidos)).toEqual({
        acao: 'lead.anonimizado',
        usuarioId: admin.id,
        de: '2026-10-01',
      }),
    )
    expect(router.state.location.search).toBe(
      `?acao=lead.anonimizado&usuario=${admin.id}&de=2026-10-01`,
    )
  })

  it('aberta pela ficha, mostra só os registros do lead, e "Ver todos" tira o filtro', async () => {
    comSessao(admin)
    const pedidos = comAuditoria()
    const { usuario } = renderizarApp('/auditoria?lead=1024')

    await tabela()
    expect(ultimoPedido(pedidos)).toEqual({ entidade: 'lead', entidadeId: '1024' })
    expect(screen.getByRole('link', { name: 'lead 1024' })).toHaveAttribute('href', '/leads/1024')

    await usuario.click(screen.getByRole('button', { name: 'Ver todos os registros' }))

    await waitFor(() => expect(ultimoPedido(pedidos)).toEqual({}))
    expect(screen.queryByRole('button', { name: 'Ver todos os registros' })).not.toBeInTheDocument()
  })

  it('pagina de 50 em 50', async () => {
    comSessao(admin)
    const pedidos = comAuditoria([registro()], 120)
    const { usuario } = renderizarApp('/auditoria')

    expect(await screen.findByText('Página 1 de 3')).toBeInTheDocument()
    await usuario.click(screen.getByRole('button', { name: 'Próxima' }))

    await waitFor(() => expect(ultimoPedido(pedidos)).toEqual({ pagina: '2' }))
    expect(await screen.findByText('Página 2 de 3')).toBeInTheDocument()
  })

  it('a atendente não vê a auditoria no menu nem abre a tela', async () => {
    comSessao(atendente)
    comQuadro()
    comLista([])
    const { router } = renderizarApp('/auditoria')

    await waitFor(() => expect(router.state.location.pathname).toBe('/funil'))
    expect(await screen.findByRole('link', { name: 'Funil' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Auditoria' })).not.toBeInTheDocument()
  })
})
