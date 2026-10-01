import type { CampoImportacao, Codificacao, Origem, Separador } from '@/api/cliente'
import { ORIGENS } from '@/features/leads/rotulos'

export const ROTULOS_CAMPO: Record<CampoImportacao, string> = {
  nao_importar: 'Não importar',
  nome: 'Nome',
  email: 'E-mail',
  telefone: 'Telefone',
  cpf_cnpj: 'CPF/CNPJ',
  uf: 'UF',
  empresa: 'Empresa',
  campanha_id: 'Campanha (id)',
  campanha_nome: 'Campanha (nome)',
  conjunto_id: 'Conjunto de anúncios (id)',
  conjunto_nome: 'Conjunto de anúncios (nome)',
  anuncio_id: 'Anúncio (id)',
  anuncio_nome: 'Anúncio (nome)',
  formulario_id: 'Formulário (id)',
  formulario_nome: 'Formulário (nome)',
  plataforma: 'Plataforma',
  utm_source: 'UTM source',
  utm_medium: 'UTM medium',
  utm_campaign: 'UTM campaign',
  utm_content: 'UTM content',
  utm_term: 'UTM term',
  gclid: 'GCLID',
  resposta_extra: 'Resposta extra',
}

export const CAMPOS = Object.keys(ROTULOS_CAMPO) as CampoImportacao[]

/** Os campos na seleção de cada coluna, em grupos. */
export const GRUPOS_DE_CAMPOS: { rotulo: string; campos: CampoImportacao[] }[] = [
  { rotulo: 'Dados do lead', campos: ['nome', 'email', 'telefone', 'cpf_cnpj', 'uf', 'empresa'] },
  {
    rotulo: 'Anúncio',
    campos: [
      'campanha_id',
      'campanha_nome',
      'conjunto_id',
      'conjunto_nome',
      'anuncio_id',
      'anuncio_nome',
      'formulario_id',
      'formulario_nome',
      'plataforma',
    ],
  },
  {
    rotulo: 'Rastreio',
    campos: ['utm_source', 'utm_medium', 'utm_campaign', 'utm_content', 'utm_term', 'gclid'],
  },
  { rotulo: 'Outros', campos: ['resposta_extra', 'nao_importar'] },
]

/**
 * Os campos que aceitam uma coluna só, como na API. O nome aceita várias (nome e sobrenome), e as respostas
 * extras e as colunas não importadas podem ser quantas forem.
 */
export function umaColunaSo(campo: CampoImportacao) {
  return campo !== 'nao_importar' && campo !== 'nome' && campo !== 'resposta_extra'
}

export const ROTULOS_CODIFICACAO: Record<Codificacao, string> = {
  'utf-8': 'UTF-8',
  'utf-16': 'UTF-16',
  'windows-1252': 'Windows-1252',
}

export const ROTULOS_SEPARADOR: Record<Separador, string> = {
  virgula: 'separado por vírgula',
  ponto_e_virgula: 'separado por ponto e vírgula',
  tab: 'separado por tab',
}

/** A importação aceita todas as origens, menos o cadastro manual. */
export const ORIGENS_DA_IMPORTACAO = ORIGENS.filter((origem) => origem !== 'manual') as Exclude<
  Origem,
  'manual'
>[]
