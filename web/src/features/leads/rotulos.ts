import type { Origem, TipoEtapa } from '@/api/cliente'

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

/** Cor do selo da etapa: ganho em destaque, perdido em vermelho, as abertas neutras. */
export function varianteEtapa(tipo: TipoEtapa) {
  if (tipo === 'ganho') {
    return 'default'
  }
  return tipo === 'perdido' ? 'destructive' : 'secondary'
}
