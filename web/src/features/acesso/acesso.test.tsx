import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { atendente, comSessao, problema } from '@/test/api'
import { comQuadro } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'

describe('login', () => {
  it('sem sessão, abrir o início leva ao login', async () => {
    comSessao(null)

    const { router } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Entrar' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('confere os campos antes de chamar a API', async () => {
    comSessao(null)
    const { usuario } = renderizarApp('/login')

    await usuario.click(await screen.findByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Informe o e-mail.')).toBeInTheDocument()
    expect(screen.getByText('Informe a senha.')).toBeInTheDocument()
  })

  it('mostra a mensagem da API quando o login falha', async () => {
    comSessao(null)
    servidor.use(
      http.post('*/api/auth/login', () =>
        problema(401, { title: 'Não foi possível entrar', detail: 'E-mail ou senha incorretos.' }),
      ),
    )
    const { usuario } = renderizarApp('/login')

    await usuario.type(await screen.findByLabelText('E-mail'), atendente.email)
    await usuario.type(screen.getByLabelText('Senha'), 'senha-errada-123')
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('E-mail ou senha incorretos.')).toBeInTheDocument()
  })

  it('entra e vai para o funil', async () => {
    comSessao(null)
    comQuadro()
    let enviado: unknown
    servidor.use(
      http.post('*/api/auth/login', async ({ request }) => {
        enviado = await request.json()
        return HttpResponse.json(atendente)
      }),
    )
    const { usuario } = renderizarApp('/login')

    await usuario.type(await screen.findByLabelText('E-mail'), atendente.email)
    await usuario.type(screen.getByLabelText('Senha'), 'senha-certa-123')
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
    expect(enviado).toEqual({ email: atendente.email, senha: 'senha-certa-123' })
  })

  it('com a senha provisória, o login leva à troca de senha', async () => {
    comSessao(null)
    servidor.use(
      http.post('*/api/auth/login', () => HttpResponse.json({ ...atendente, trocarSenha: true })),
    )
    const { usuario } = renderizarApp('/login')

    await usuario.type(await screen.findByLabelText('E-mail'), atendente.email)
    await usuario.type(screen.getByLabelText('Senha'), 'provisoria-123')
    await usuario.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Trocar senha' })).toBeInTheDocument()
    expect(screen.getByText(/Sua senha é provisória/)).toBeInTheDocument()
  })
})

describe('troca de senha', () => {
  async function preencher(
    usuario: ReturnType<typeof renderizarApp>['usuario'],
    atual: string,
    nova: string,
    confirmacao: string,
  ) {
    await usuario.type(await screen.findByLabelText('Senha provisória'), atual)
    await usuario.type(screen.getByLabelText('Nova senha'), nova)
    await usuario.type(screen.getByLabelText('Repita a nova senha'), confirmacao)
    await usuario.click(screen.getByRole('button', { name: 'Salvar nova senha' }))
  }

  it('com a senha provisória, qualquer rota leva à troca de senha', async () => {
    comSessao({ ...atendente, trocarSenha: true })

    const { router } = renderizarApp('/')

    expect(await screen.findByRole('heading', { name: 'Trocar senha' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/trocar-senha')
  })

  it('exige 10 caracteres na nova senha', async () => {
    comSessao({ ...atendente, trocarSenha: true })
    const { usuario } = renderizarApp('/trocar-senha')

    await preencher(usuario, 'provisoria-123', 'curta', 'curta')

    expect(
      await screen.findByText('A senha precisa ter pelo menos 10 caracteres.'),
    ).toBeInTheDocument()
  })

  it('exige a confirmação igual à nova senha', async () => {
    comSessao({ ...atendente, trocarSenha: true })
    const { usuario } = renderizarApp('/trocar-senha')

    await preencher(usuario, 'provisoria-123', 'nova-senha-123', 'nova-senha-456')

    expect(await screen.findByText('As duas senhas não são iguais.')).toBeInTheDocument()
  })

  it('mostra no campo o erro que a API devolve', async () => {
    comSessao({ ...atendente, trocarSenha: true })
    servidor.use(
      http.post('*/api/auth/trocar-senha', () =>
        problema(400, { errors: { senhaAtual: ['Senha atual incorreta.'] } }),
      ),
    )
    const { usuario } = renderizarApp('/trocar-senha')

    await preencher(usuario, 'senha-errada-1', 'nova-senha-123', 'nova-senha-123')

    expect(await screen.findByText('Senha atual incorreta.')).toBeInTheDocument()
    expect(screen.getByLabelText('Senha provisória')).toHaveAttribute('aria-invalid', 'true')
  })

  it('depois de trocar, libera o sistema', async () => {
    comSessao({ ...atendente, trocarSenha: true })
    comQuadro()
    servidor.use(
      http.post('*/api/auth/trocar-senha', () => new HttpResponse(null, { status: 204 })),
    )
    const { usuario } = renderizarApp('/trocar-senha')

    await preencher(usuario, 'provisoria-123', 'nova-senha-123', 'nova-senha-123')

    expect(await screen.findByRole('heading', { name: 'Funil' })).toBeInTheDocument()
  })
})
