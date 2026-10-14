import type { Origem } from '@/api/cliente'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { formatarDia } from '@/lib/datas'

/** Os nomes que a tela conhece, para mostrar quem é cada id dos registros. Um id desconhecido aparece como está. */
export type Nomes = {
  usuario: (id: string) => string | undefined
  etapa: (id: number) => string | undefined
  motivo: (id: number) => string | undefined
  campanha: (id: number) => string | undefined
}

/** As ações da auditoria, agrupadas como no filtro. A API grava o código; a tela mostra o rótulo. */
export const GRUPOS_DE_ACOES: { titulo: string; acoes: [codigo: string, rotulo: string][] }[] = [
  {
    titulo: 'Acesso',
    acoes: [
      ['login.sucesso', 'Login'],
      ['login.falha', 'Login recusado'],
      ['senha.trocada', 'Troca de senha'],
    ],
  },
  {
    titulo: 'Leads',
    acoes: [
      ['lead.criado', 'Lead criado'],
      ['lead.editado', 'Lead editado'],
      ['lead.reatribuido', 'Troca de responsável'],
      ['importacao.criada', 'Importação de CSV'],
    ],
  },
  {
    titulo: 'LGPD e exportações',
    acoes: [
      ['lead.dados_exportados', 'Dados do titular exportados'],
      ['lead.anonimizado', 'Lead anonimizado'],
      ['lead.excluido', 'Lead de teste excluído'],
      ['leads.exportados', 'Leads exportados em CSV'],
    ],
  },
  {
    titulo: 'Usuários',
    acoes: [
      ['usuario.criado', 'Usuário criado'],
      ['usuario.editado', 'Usuário editado'],
      ['usuario.senha_redefinida', 'Senha redefinida'],
    ],
  },
  {
    titulo: 'Configurações',
    acoes: [
      ['etapa.criada', 'Etapa criada'],
      ['etapa.editada', 'Etapa renomeada'],
      ['etapas.reordenadas', 'Etapas reordenadas'],
      ['etapa.desativada', 'Etapa desativada'],
      ['etapa.reativada', 'Etapa reativada'],
      ['motivo_perda.criado', 'Motivo de perda criado'],
      ['motivo_perda.editado', 'Motivo de perda editado'],
      ['campanha.renomeada', 'Campanha renomeada'],
      ['configuracao.alterada', 'Configuração alterada'],
    ],
  },
  { titulo: 'Sistema', acoes: [['demonstracao.preparada', 'Demonstração preparada']] },
]

const ROTULOS_ACAO = new Map(GRUPOS_DE_ACOES.flatMap((grupo) => grupo.acoes))

export function rotuloAcao(acao: string) {
  return ROTULOS_ACAO.get(acao) ?? acao
}

const CAMPOS: Record<string, string> = {
  nome: 'nome',
  email: 'e-mail',
  telefone: 'telefone',
  cpfCnpj: 'CPF/CNPJ',
  uf: 'UF',
  empresa: 'empresa',
  perfil: 'perfil',
  ativo: 'ativo',
  participaRodizio: 'rodízio',
}

const MOTIVOS_LOGIN: Record<string, string> = {
  senha_incorreta: 'Senha incorreta',
  bloqueado: 'Bloqueado por tentativas erradas',
  desativado: 'Usuário desativado',
  usuario_desconhecido: 'E-mail desconhecido',
}

const CONFIGURACOES: Record<string, string> = {
  distribuicao_automatica: 'Distribuição automática (rodízio)',
  exigir_documento_para_ganho: 'CPF/CNPJ para Ganho',
}

/** O registro afetado, sem dados pessoais: os leads aparecem pelo número. */
export function descreverEntidade(entidade: string, entidadeId: string | null, nomes: Nomes) {
  const id = entidadeId ?? ''
  switch (entidade) {
    case 'lead':
      return `Lead ${id}`
    case 'importacao':
      return `Importação ${id}`
    case 'usuario':
      return nomes.usuario(id) ?? 'Usuário'
    case 'etapa':
      return entidadeId ? `Etapa ${nomes.etapa(Number(id)) ?? id}` : 'Etapas'
    case 'motivo_perda':
      return `Motivo de perda ${nomes.motivo(Number(id)) ?? id}`
    case 'campanha':
      return `Campanha ${nomes.campanha(Number(id)) ?? id}`
    case 'configuracao':
      return CONFIGURACOES[id] ?? id
    default:
      return `${entidade} ${id}`.trim()
  }
}

type Objeto = Record<string, unknown>

const ehObjeto = (valor: unknown): valor is Objeto =>
  typeof valor === 'object' && valor !== null && !Array.isArray(valor)

const numero = (valor: unknown) => (typeof valor === 'number' ? valor : 0)

const texto = (valor: unknown) => (typeof valor === 'string' ? valor : '')

function contar(quantidade: number, singular: string, plural: string) {
  return `${quantidade} ${quantidade === 1 ? singular : plural}`
}

function origem(valor: unknown) {
  return ROTULOS_ORIGEM[valor as Origem] ?? texto(valor)
}

function campos(valor: unknown) {
  const lista = Array.isArray(valor)
    ? valor.map((campo) => CAMPOS[texto(campo)] ?? texto(campo))
    : []
  return lista.length > 0 ? `Campos: ${lista.join(', ')}` : null
}

/** Os filtros de uma exportação. Da busca, a API guarda só que houve uma: o texto não fica na auditoria. */
function filtros(valor: unknown, nomes: Nomes) {
  if (!ehObjeto(valor) || Object.keys(valor).length === 0) {
    return 'sem filtros'
  }
  const partes: string[] = []
  if (valor.busca) {
    partes.push('busca')
  }
  if (typeof valor.etapaId === 'number') {
    partes.push(`etapa ${nomes.etapa(valor.etapaId) ?? valor.etapaId}`)
  }
  if (valor.origem) {
    partes.push(`origem ${origem(valor.origem)}`)
  }
  if (typeof valor.campanhaId === 'number') {
    partes.push(`campanha ${valor.campanhaId}`)
  }
  if (valor.semResponsavel) {
    partes.push('sem responsável')
  }
  if (typeof valor.responsavelId === 'string') {
    partes.push(`responsável ${nomes.usuario(valor.responsavelId) ?? valor.responsavelId}`)
  }
  if (typeof valor.de === 'string') {
    partes.push(`entrada a partir de ${formatarDia(valor.de)}`)
  }
  if (typeof valor.ate === 'string') {
    partes.push(`entrada até ${formatarDia(valor.ate)}`)
  }
  return partes.join(', ')
}

/** Um valor dos detalhes que a tela não conhece, como veio. */
function generico(detalhes: Objeto) {
  const partes = Object.entries(detalhes).map(([chave, valor]) => {
    const legivel =
      typeof valor === 'boolean'
        ? valor
          ? 'sim'
          : 'não'
        : typeof valor === 'string'
          ? valor
          : JSON.stringify(valor)
    return `${chave}: ${legivel}`
  })
  return partes.length > 0 ? partes.join(' · ') : null
}

/** Os detalhes do registro em português, com os nomes que a tela conhece. Os detalhes nunca têm dados pessoais. */
export function descreverDetalhes(acao: string, detalhes: unknown, nomes: Nomes): string | null {
  if (!ehObjeto(detalhes)) {
    return null
  }
  const pessoa = (id: unknown) =>
    typeof id === 'string' ? (nomes.usuario(id) ?? 'usuário removido') : 'ninguém'
  const etapa = (id: unknown) =>
    typeof id === 'number' ? (nomes.etapa(id) ?? `etapa ${id}`) : 'outra etapa'

  switch (acao) {
    case 'login.falha':
      return MOTIVOS_LOGIN[texto(detalhes.motivo)] ?? (texto(detalhes.motivo) || null)
    case 'lead.criado':
      return `Origem: ${origem(detalhes.origem)}`
    case 'importacao.criada':
      return `${contar(numero(detalhes.linhas), 'linha', 'linhas')}, origem ${origem(detalhes.origem)}`
    case 'lead.editado':
    case 'usuario.editado':
    case 'etapa.editada':
    case 'motivo_perda.editado':
    case 'campanha.renomeada':
      return campos(detalhes.campos)
    case 'lead.reatribuido':
      return `De ${pessoa(detalhes.anteriorId)} para ${pessoa(detalhes.novoId)}`
    case 'usuario.criado':
      return `Perfil ${texto(detalhes.perfil)}${detalhes.participaRodizio ? ', no rodízio' : ''}`
    case 'etapa.criada':
    case 'motivo_perda.criado':
      return `Nome: ${texto(detalhes.nome)}`
    case 'etapas.reordenadas':
      return Array.isArray(detalhes.ids)
        ? `Nova ordem: ${detalhes.ids.map(etapa).join(', ')}`
        : null
    case 'etapa.desativada':
      return numero(detalhes.leadsMovidos) > 0
        ? `${contar(numero(detalhes.leadsMovidos), 'lead movido', 'leads movidos')} para ${etapa(detalhes.destinoId)}`
        : 'Sem leads para mover'
    case 'configuracao.alterada':
      return detalhes.valor ? 'Ligada' : 'Desligada'
    case 'lead.anonimizado':
      return (
        `${contar(numero(detalhes.entradas), 'entrada', 'entradas')} e ` +
        `${contar(numero(detalhes.interacoes), 'interação', 'interações')} limpas; ` +
        `${contar(numero(detalhes.eventos), 'aviso ao n8n apagado', 'avisos ao n8n apagados')}`
      )
    case 'leads.exportados':
      return `${contar(numero(detalhes.linhas), 'lead', 'leads')}, ${filtros(detalhes.filtros, nomes)}`
    case 'demonstracao.preparada':
      return (
        `${contar(numero(detalhes.leads), 'lead', 'leads')} e ` +
        `${contar(numero(detalhes.entradas), 'entrada', 'entradas')} da demonstração, no lugar de ` +
        `${contar(numero(detalhes.leadsApagados), 'lead apagado', 'leads apagados')}`
      )
    default:
      return generico(detalhes)
  }
}
