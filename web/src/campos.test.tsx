import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { UsuarioResponse } from '@/api/cliente'
import { admin, atendente, comSessao } from '@/test/api'
import { comAuditoria } from '@/test/auditoria'
import { comImportacao } from '@/test/importacao'
import { comCadastros, comLista, comQuadro, fichaLead, leadResumo, usuarios } from '@/test/leads'
import { comPainel } from '@/test/painel'
import { comRelatorio } from '@/test/relatorio'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

/**
 * Os campos de formulário na página sem `id` nem `name`. O Chrome acusa cada um no DevTools
 * (FormEmptyIdAndNameAttributesForInputError) e não consegue preenchê-los sozinho. Entram também os inputs escondidos
 * que o Radix cria para a seleção e o interruptor dentro de um `<form>`.
 */
function camposSemIdNemName() {
  return [...document.querySelectorAll('input, select, textarea')]
    .filter((campo) => !campo.id && !campo.getAttribute('name'))
    .map((campo) => campo.outerHTML)
}

describe('todo campo de formulário tem id ou name', () => {
  it('na lista de leads, com a busca e os filtros', async () => {
    comSessao(admin)
    comCadastros()
    comLista([leadResumo()])

    renderizarApp('/leads')

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('no funil, no quadro e na lista do celular', async () => {
    comSessao(atendente)
    comQuadro()

    renderizarApp('/funil')

    expect(await screen.findByRole('region', { name: 'Leads por etapa' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('na ficha, com o registro de interação e o motivo da perda', async () => {
    comSessao(admin)
    comCadastros()
    servidor.use(
      http.get('*/api/leads/:id', () => HttpResponse.json(fichaLead())),
      http.get('*/api/leads/:id/linha-do-tempo', () => HttpResponse.json([])),
    )
    const { usuario } = renderizarApp('/leads/1024')

    expect(await screen.findByRole('heading', { name: 'Maria Souza' })).toBeInTheDocument()
    await screen.findByRole('option', { name: 'Perdido' })
    expect(camposSemIdNemName()).toEqual([])

    await usuario.click(screen.getByRole('button', { name: 'Registrar interação' }))
    const interacao = within(await screen.findByRole('dialog', { name: 'Registrar interação' }))
    expect(camposSemIdNemName()).toEqual([])
    await usuario.click(interacao.getByRole('button', { name: 'Cancelar' }))

    await usuario.selectOptions(screen.getByLabelText('Mudar para'), 'Perdido')
    await screen.findByRole('dialog', { name: 'Motivo da perda' })
    expect(camposSemIdNemName()).toEqual([])
  })

  it('no novo lead', async () => {
    comSessao(admin)
    comCadastros()

    renderizarApp('/leads/novo')

    expect(await screen.findByRole('heading', { name: 'Novo lead' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('no painel e no relatório', async () => {
    comSessao(admin)
    comPainel()
    comRelatorio()

    const { router } = renderizarApp('/painel')
    expect(await screen.findByRole('heading', { name: 'Painel' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])

    await router.navigate('/relatorio')
    expect(
      await screen.findByRole('heading', { name: 'Relatório por campanha' }),
    ).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('na auditoria e na importação', async () => {
    comSessao(admin)
    comAuditoria()
    comImportacao()

    const { router } = renderizarApp('/auditoria')
    expect(await screen.findByRole('heading', { name: 'Auditoria' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])

    await router.navigate('/importacao')
    expect(await screen.findByRole('heading', { name: 'Importação de leads' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('nos usuários, com os diálogos de novo usuário e de edição', async () => {
    comSessao(admin)
    const adriana = usuarios.find((u: UsuarioResponse) => u.id === atendente.id)!
    servidor.use(
      http.get('*/api/usuarios', () => HttpResponse.json(usuarios)),
      http.get('*/api/configuracoes', () =>
        HttpResponse.json({ exigirDocumentoParaGanho: true, distribuicaoAutomatica: true }),
      ),
    )
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])

    await usuario.click(screen.getByRole('button', { name: 'Novo usuário' }))
    const novo = within(await screen.findByRole('dialog'))
    expect(camposSemIdNemName()).toEqual([])
    await usuario.click(novo.getByRole('button', { name: 'Cancelar' }))

    await usuario.click(
      within(screen.getByRole('table')).getByRole('button', { name: `Ações de ${adriana.nome}` }),
    )
    await usuario.click(await screen.findByRole('menuitem', { name: 'Editar' }))
    expect(await screen.findByRole('switch', { name: 'Ativo' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('nas etapas e motivos', async () => {
    comSessao(admin)
    comCadastros()
    servidor.use(
      http.get('*/api/configuracoes', () =>
        HttpResponse.json({ exigirDocumentoParaGanho: true, distribuicaoAutomatica: true }),
      ),
    )

    renderizarApp('/configuracoes/funil')

    expect(await screen.findByRole('heading', { name: 'Etapas e motivos' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('no login e na troca de senha', async () => {
    comSessao(null)
    renderizarApp('/login')
    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })

  it('na troca de senha obrigatória', async () => {
    comSessao({ ...atendente, trocarSenha: true })
    renderizarApp('/trocar-senha')
    expect(await screen.findByRole('heading', { name: 'Trocar senha' })).toBeInTheDocument()
    expect(camposSemIdNemName()).toEqual([])
  })
})
