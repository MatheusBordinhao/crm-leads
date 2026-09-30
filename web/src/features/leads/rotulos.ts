import type { Origem, TipoEtapa, TipoInteracao } from '@/api/cliente'

export const ROTULOS_ORIGEM: Record<Origem, string> = {
  meta_lead_ads: 'Meta (Facebook e Instagram)',
  google_ads: 'Google Ads',
  site: 'Site',
  whatsapp: 'WhatsApp',
  telefone: 'Telefone',
  indicacao: 'Indicação',
  planilha: 'Planilha',
  manual: 'Cadastro manual',
}

export const ORIGENS = Object.keys(ROTULOS_ORIGEM) as Origem[]

/** Origens que a pessoa escolhe ao cadastrar um lead à mão. */
export const ORIGENS_DO_CADASTRO = [
  'manual',
  'telefone',
  'whatsapp',
  'indicacao',
] as const satisfies readonly Origem[]

const ROTULOS_PLATAFORMA: Record<string, string> = {
  facebook: 'Facebook',
  instagram: 'Instagram',
  google: 'Google',
  site: 'Site',
}

export function rotuloPlataforma(plataforma: string) {
  return ROTULOS_PLATAFORMA[plataforma] ?? plataforma
}

export const ROTULOS_INTERACAO: Record<TipoInteracao, string> = {
  nota: 'Nota',
  ligacao: 'Ligação',
  whatsapp: 'WhatsApp',
  email: 'E-mail',
  reuniao: 'Reunião',
}

export const TIPOS_INTERACAO = [
  'nota',
  'ligacao',
  'whatsapp',
  'email',
  'reuniao',
] as const satisfies readonly TipoInteracao[]

/** Para a linha do tempo: "Adriana registrou uma ligação". */
export const INTERACAO_COM_ARTIGO: Record<TipoInteracao, string> = {
  nota: 'uma nota',
  ligacao: 'uma ligação',
  whatsapp: 'um WhatsApp',
  email: 'um e-mail',
  reuniao: 'uma reunião',
}

/** Cor do selo da etapa: ganho em destaque, perdido em vermelho, as abertas neutras. */
export function varianteEtapa(tipo: TipoEtapa) {
  if (tipo === 'ganho') {
    return 'default'
  }
  return tipo === 'perdido' ? 'destructive' : 'secondary'
}
