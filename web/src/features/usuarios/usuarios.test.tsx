import { screen, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import type { UsuarioResponse } from '@/api/cliente'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comQuadro } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

const eu: UsuarioResponse = {
  id: admin.id,
  nome: admin.nome,
  email: admin.email,
  perfil: 'Admin',
  ativo: true,
  participaRodizio: false,
  trocarSenha: false,
  bloqueadoAte: null,
  ultimoLeadAtribuidoEm: null,
  criadoEm: '2026-09-28T12:00:00Z',
}

const adriana: UsuarioResponse = {
  id: atendente.id,
  nome: 'Adriana',
  email: atendente.email,
  perfil: 'Atendente',
  ativo: true,
  participaRodizio: true,
  trocarSenha: true,
  bloqueadoAte: '2026-10-05T17:32:00Z',
  ultimoLeadAtribuidoEm: null,
  criadoEm: '2026-09-28T12:00:00Z',
}

/** Admin logado vendo a lista. Devolve as requisições de escrita recebidas pela API simulada. */
function comLista(usuarios: UsuarioResponse[] = [eu, adriana]) {
  comSessao(admin)
  const recebidas: { metodo: string; caminho: string; corpo: unknown }[] = []
  const registrar = async ({ request }: { request: Request }) => {
    recebidas.push({
      metodo: request.method,
      caminho: new URL(request.url).pathname,
      corpo: await request.json(),
    })
  }
  servidor.use(
    http.get('*/api/usuarios', () => HttpResponse.json(usuarios)),
    http.put('*/api/usuarios/:id', async (info) => {
      await registrar(info)
      return HttpResponse.json(adriana)
    }),
    http.post('*/api/usuarios/:id/redefinir-senha', async (info) => {
      await registrar(info)
      return new HttpResponse(null, { status: 204 })
    }),
  )
  return recebidas
}

async function tabela() {
  return within(await screen.findByRole('table'))
}

describe('usuários', () => {
  it('atendente que abre a tela volta ao funil', async () => {
    comSessao(atendente)
    comQuadro()

    const { router } = renderizarApp('/configuracoes/usuarios')

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/funil')
  })

  it('lista perfil, bloqueio e senha provisória', async () => {
    comLista()

    renderizarApp('/configuracoes/usuarios')

    const linha = within((await tabela()).getByText('Adriana').closest('tr')!)
    expect(linha.getByText('Atendente')).toBeInTheDocument()
    expect(linha.getByText('Bloqueado até 14:32')).toBeInTheDocument()
    expect(linha.getByText('Senha provisória')).toBeInTheDocument()
    expect(linha.getByRole('switch', { name: 'Rodízio: Adriana' })).toBeChecked()
  })

  it('o admin não consegue desativar a si mesmo pela lista', async () => {
    comLista()

    renderizarApp('/configuracoes/usuarios')

    const t = await tabela()
    expect(t.getByRole('switch', { name: `Ativo: ${admin.nome}` })).toBeDisabled()
    expect(t.getByRole('switch', { name: 'Ativo: Adriana' })).toBeEnabled()
  })

  it('desativa pelo interruptor da lista', async () => {
    const recebidas = comLista()
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click((await tabela()).getByRole('switch', { name: 'Ativo: Adriana' }))

    expect(await screen.findByText('Adriana foi desativado.')).toBeInTheDocument()
    expect(recebidas).toEqual([
      {
        metodo: 'PUT',
        caminho: `/api/usuarios/${adriana.id}`,
        corpo: {
          nome: 'Adriana',
          email: adriana.email,
          perfil: 'Atendente',
          ativo: false,
          participaRodizio: true,
        },
      },
    ])
  })

  it('cria um usuário e mostra a senha provisória para enviar', async () => {
    comLista()
    let enviado: Record<string, unknown> | undefined
    servidor.use(
      http.post('*/api/usuarios', async ({ request }) => {
        enviado = (await request.json()) as Record<string, unknown>
        return HttpResponse.json({ ...adriana, id: 'novo', nome: 'Pietra' }, { status: 201 })
      }),
    )
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click(await screen.findByRole('button', { name: 'Novo usuário' }))
    const dialogo = within(await screen.findByRole('dialog'))
    await usuario.type(dialogo.getByLabelText('Nome'), 'Pietra')
    await usuario.type(dialogo.getByLabelText('E-mail'), 'matheusbordinhao11+pietra@gmail.com')
    await usuario.click(dialogo.getByRole('button', { name: 'Gerar' }))
    const senha = (dialogo.getByLabelText('Senha provisória') as HTMLInputElement).value
    await usuario.click(dialogo.getByRole('button', { name: 'Criar usuário' }))

    expect(await dialogo.findByText(senha)).toBeInTheDocument()
    expect(dialogo.getByRole('heading', { name: 'Usuário criado' })).toBeInTheDocument()
    expect(senha).toMatch(/^\w{4}-\w{4}-\w{4}$/)
    expect(enviado).toEqual({
      nome: 'Pietra',
      email: 'matheusbordinhao11+pietra@gmail.com',
      perfil: 'Atendente',
      participaRodizio: true,
      senhaProvisoria: senha,
    })
  })

  it('confere os campos do novo usuário antes de chamar a API', async () => {
    comLista()
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click(await screen.findByRole('button', { name: 'Novo usuário' }))
    const dialogo = within(await screen.findByRole('dialog'))
    await usuario.type(dialogo.getByLabelText('E-mail'), 'sem-arroba')
    await usuario.clear(dialogo.getByLabelText('Senha provisória'))
    await usuario.type(dialogo.getByLabelText('Senha provisória'), 'curta')
    await usuario.click(dialogo.getByRole('button', { name: 'Criar usuário' }))

    expect(await dialogo.findByText('Informe o nome.')).toBeInTheDocument()
    expect(dialogo.getByText('E-mail inválido.')).toBeInTheDocument()
    expect(dialogo.getByText('A senha precisa ter pelo menos 10 caracteres.')).toBeInTheDocument()
  })

  it('mostra no campo o erro de e-mail repetido que a API devolve', async () => {
    comLista()
    servidor.use(
      http.post('*/api/usuarios', () =>
        problema(400, { errors: { email: ['Este e-mail já está em uso.'] } }),
      ),
    )
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click(await screen.findByRole('button', { name: 'Novo usuário' }))
    const dialogo = within(await screen.findByRole('dialog'))
    await usuario.type(dialogo.getByLabelText('Nome'), 'Adriana')
    await usuario.type(dialogo.getByLabelText('E-mail'), adriana.email)
    await usuario.click(dialogo.getByRole('button', { name: 'Criar usuário' }))

    expect(await dialogo.findByText('Este e-mail já está em uso.')).toBeInTheDocument()
  })

  it('edita o perfil e o nome', async () => {
    const recebidas = comLista()
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click((await tabela()).getByRole('button', { name: 'Ações de Adriana' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Editar' }))
    const dialogo = within(await screen.findByRole('dialog'))
    await usuario.clear(dialogo.getByLabelText('Nome'))
    await usuario.type(dialogo.getByLabelText('Nome'), 'Adriana Souza')
    await usuario.click(dialogo.getByRole('radio', { name: 'Admin' }))
    await usuario.click(dialogo.getByRole('button', { name: 'Salvar' }))

    expect(await screen.findByText('Usuário salvo.')).toBeInTheDocument()
    expect(recebidas[0].corpo).toEqual({
      nome: 'Adriana Souza',
      email: adriana.email,
      perfil: 'Admin',
      ativo: true,
      participaRodizio: true,
    })
  })

  it('redefine a senha e mostra a nova senha provisória', async () => {
    const recebidas = comLista()
    const { usuario } = renderizarApp('/configuracoes/usuarios')

    await usuario.click((await tabela()).getByRole('button', { name: 'Ações de Adriana' }))
    await usuario.click(await screen.findByRole('menuitem', { name: 'Redefinir senha' }))
    const dialogo = within(await screen.findByRole('dialog'))
    const senha = (dialogo.getByLabelText('Senha provisória') as HTMLInputElement).value
    await usuario.click(dialogo.getByRole('button', { name: 'Redefinir senha' }))

    expect(await dialogo.findByText(senha)).toBeInTheDocument()
    expect(recebidas).toEqual([
      {
        metodo: 'POST',
        caminho: `/api/usuarios/${adriana.id}/redefinir-senha`,
        corpo: { senhaProvisoria: senha },
      },
    ])
  })
})
