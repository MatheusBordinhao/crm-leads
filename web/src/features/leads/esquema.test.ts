import { describe, expect, it } from 'vitest'
import { z } from '@/lib/zod'
import { camposDados, MENSAGENS, validarDados } from './esquema'

const esquema = z.object(camposDados).superRefine(validarDados())

function errosDoEmail(email: string) {
  const resultado = esquema.safeParse({
    nome: 'Maria Souza',
    email,
    telefone: '',
    cpfCnpj: '',
    uf: '',
    empresa: '',
  })
  return resultado.success
    ? []
    : resultado.error.issues.filter((erro) => erro.path[0] === 'email').map((erro) => erro.message)
}

// Os mesmos casos do teste da API (EmailsUfsTextosTests): os dois formatos são iguais.
describe('formato do e-mail do lead', () => {
  it.each([
    'maria.souza+orcamento@empresa-x.com.br',
    'joao_silva@sub.empresa.com.br',
    "o'connor@example.com",
    'maria@xn--exemplo-9za.com',
    'contato@123.com.br',
    ' Maria@EmpresaX.com.BR ',
  ])('%s é aceito', (email) => {
    expect(errosDoEmail(email)).toEqual([])
  })

  it.each([
    'testenotificação@exemplo.com',
    'maria@exemplação.com',
    'maria,souza@empresa.com',
    '"maria"@empresa.com',
    '.maria@empresa.com',
    'maria.@empresa.com',
    'maria..souza@empresa.com',
    'maria@empresa..com',
    'maria@-empresa.com',
    'maria@empresa-.com',
    'maria@empresa_x.com',
    'maria@@empresa.com',
    'maria@empresax',
  ])('%s é recusado', (email) => {
    expect(errosDoEmail(email)).toEqual([MENSAGENS.email])
  })
})
