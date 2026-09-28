import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { servidor } from '@/test/servidor'
import { api, ErroApi, exigirSucesso } from './cliente'

describe('cliente da API', () => {
  it('devolve os dados quando a resposta é 2xx', async () => {
    servidor.use(
      http.get('*/api/auth/eu', () =>
        HttpResponse.json({
          id: '5b1e0000-0000-0000-0000-000000000001',
          nome: 'Adriana',
          email: 'adriana@example.com',
          perfil: 'Atendente',
          trocarSenha: false,
        }),
      ),
    )

    const eu = await exigirSucesso(api.GET('/api/auth/eu'))

    expect(eu.nome).toBe('Adriana')
  })

  it('lança ErroApi com a mensagem e os erros de cada campo do ProblemDetails', async () => {
    servidor.use(
      http.post('*/api/usuarios', () =>
        HttpResponse.json(
          {
            status: 400,
            title: 'Requisição inválida',
            errors: { email: ['Este e-mail já está em uso.'] },
          },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const erro = await exigirSucesso(
      api.POST('/api/usuarios', {
        body: {
          nome: 'Adriana',
          email: 'adriana@example.com',
          perfil: 'Atendente',
          participaRodizio: true,
          senhaProvisoria: 'provisoria-123',
        },
      }),
    ).catch((e: unknown) => e)

    expect(erro).toBeInstanceOf(ErroApi)
    expect((erro as ErroApi).status).toBe(400)
    expect((erro as ErroApi).message).toBe('Requisição inválida')
    expect((erro as ErroApi).errosDosCampos).toEqual({ email: ['Este e-mail já está em uso.'] })
  })

  it('usa o detail como mensagem quando existe', async () => {
    servidor.use(
      http.post('*/api/auth/login', () =>
        HttpResponse.json(
          { status: 401, title: 'Não foi possível entrar', detail: 'E-mail ou senha incorretos.' },
          { status: 401, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const erro = await exigirSucesso(
      api.POST('/api/auth/login', { body: { email: 'a@example.com', senha: 'x' } }),
    ).catch((e: unknown) => e)

    expect((erro as ErroApi).message).toBe('E-mail ou senha incorretos.')
  })
})
