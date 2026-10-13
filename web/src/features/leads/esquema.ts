import type { LeadResponse } from '@/api/cliente'
import { documentoValido, normalizarDocumento } from '@/lib/documento'
import { formatarTelefone } from '@/lib/telefone'
import { z } from '@/lib/zod'

export const MENSAGENS = {
  contato: 'Informe o e-mail ou o telefone.',
  email: 'E-mail inválido.',
  telefone: 'Telefone inválido. Informe com DDD, como (51) 99999-8888.',
  documento: 'CPF ou CNPJ inválido.',
}

/** O mesmo formato que a API confere. */
const FORMATO_EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/

/** Campos de dados pessoais, iguais no novo lead e na ficha. */
export const camposDados = {
  nome: z.string().trim().min(1, 'Informe o nome.').max(150, 'O nome pode ter até 150 caracteres.'),
  email: z.string().trim(),
  telefone: z.string().trim(),
  cpfCnpj: z.string().trim(),
  uf: z.string(),
  empresa: z.string().trim().max(150, 'A empresa pode ter até 150 caracteres.'),
}

type DadosPessoais = { [campo in keyof typeof camposDados]: string }

type Originais = Pick<LeadResponse, 'email' | 'telefone' | 'cpfCnpj'>

function emailNormalizado(email: string) {
  return email.replace(/\s/g, '').toLowerCase()
}

/**
 * A validação exata do telefone é a do libphonenumber, na API, e o erro dela aparece no campo. Aqui só
 * barra o que claramente não é telefone: com DDD são 10 ou 11 dígitos, mais o 55 se vier com o país.
 */
function telefonePlausivel(telefone: string) {
  const digitos = telefone.replace(/\D/g, '').length
  return telefone.startsWith('+') ? digitos >= 8 && digitos <= 15 : digitos >= 10 && digitos <= 13
}

/**
 * Validação estrita da tela (seção 3.1). Na edição, com os `originais`, o formato só é conferido nos
 * campos que mudaram: um valor inválido que veio da integração não impede salvar o resto.
 */
export function validarDados(originais?: Originais) {
  const mudou = (campo: keyof Originais, valor: string) =>
    originais === undefined || valor !== (originais[campo] ?? '')

  return (dados: DadosPessoais, contexto: z.RefinementCtx) => {
    const erro = (campo: keyof DadosPessoais, mensagem: string) =>
      contexto.addIssue({ code: 'custom', path: [campo], message: mensagem })

    if (!dados.email && !dados.telefone) {
      erro('email', MENSAGENS.contato)
    }
    const email = emailNormalizado(dados.email)
    if (email && mudou('email', email) && !FORMATO_EMAIL.test(email)) {
      erro('email', MENSAGENS.email)
    }
    if (dados.telefone && mudou('telefone', dados.telefone) && !telefonePlausivel(dados.telefone)) {
      erro('telefone', MENSAGENS.telefone)
    }
    const documento = normalizarDocumento(dados.cpfCnpj)
    if (dados.cpfCnpj && mudou('cpfCnpj', documento) && !documentoValido(documento)) {
      erro('cpfCnpj', MENSAGENS.documento)
    }
  }
}

/** Os valores da ficha como o formulário os mostra, para a validação saber o que a pessoa mudou. */
export function originaisDaFicha(lead: LeadResponse): Originais {
  return {
    email: lead.email,
    telefone: lead.telefone ? formatarTelefone(lead.telefone) : null,
    cpfCnpj: lead.cpfCnpj,
  }
}

/**
 * Valores iniciais do formulário a partir da ficha (ou vazios, no novo lead). O telefone do Brasil aparece no formato
 * nacional: a API o normaliza de volta para o mesmo E.164 e não conta como mudança.
 */
export function dadosIniciais(lead?: LeadResponse): DadosPessoais {
  return {
    nome: lead?.nome ?? '',
    email: lead?.email ?? '',
    telefone: lead?.telefone ? formatarTelefone(lead.telefone) : '',
    cpfCnpj: lead?.cpfCnpj ?? '',
    uf: lead?.uf ?? '',
    empresa: lead?.empresa ?? '',
  }
}

/** Campo vazio vai como `null`; a API normaliza o resto. */
export function dadosParaApi(dados: DadosPessoais) {
  const ouNulo = (valor: string) => valor.trim() || null
  return {
    nome: dados.nome.trim(),
    email: ouNulo(dados.email),
    telefone: ouNulo(dados.telefone),
    cpfCnpj: ouNulo(dados.cpfCnpj),
    uf: ouNulo(dados.uf),
    empresa: ouNulo(dados.empresa),
  }
}

export const CAMPOS_DADOS = Object.keys(camposDados) as (keyof DadosPessoais)[]
