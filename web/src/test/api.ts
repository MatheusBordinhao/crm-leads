import { http, HttpResponse } from 'msw'
import type { EuResponse } from '@/api/cliente'
import { servidor } from './servidor'

export const atendente: EuResponse = {
  id: '5b1e0000-0000-0000-0000-000000000001',
  nome: 'Adriana',
  email: 'matheusbordinhao11+adriana@gmail.com',
  perfil: 'Atendente',
  trocarSenha: false,
}

export const admin: EuResponse = {
  id: '5b1e0000-0000-0000-0000-000000000002',
  nome: 'Matheus',
  email: 'matheusbordinhao11@gmail.com',
  perfil: 'Admin',
  trocarSenha: false,
}

/** Resposta de erro no formato da API (ProblemDetails). */
export function problema(status: number, corpo: Record<string, unknown> = {}) {
  return HttpResponse.json(
    { status, ...corpo },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

/** Define quem está logado; `null` simula a falta de sessão (401). */
export function comSessao(eu: EuResponse | null) {
  servidor.use(http.get('*/api/auth/eu', () => (eu ? HttpResponse.json(eu) : problema(401))))
}
