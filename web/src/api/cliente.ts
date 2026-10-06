import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

type Schemas = components['schemas']
export type Perfil = Schemas['Perfil']
export type EuResponse = Schemas['EuResponse']
export type UsuarioResponse = Schemas['UsuarioResponse']
export type CriarUsuarioRequest = Schemas['CriarUsuarioRequest']
export type EditarUsuarioRequest = Schemas['EditarUsuarioRequest']
export type Origem = Schemas['Origem']
export type TipoEtapa = Schemas['TipoEtapa']
export type OrdemLeads = Schemas['OrdemLeads']
export type EtapaResponse = Schemas['EtapaResponse']
export type CampanhaResponse = Schemas['CampanhaResponse']
export type LeadResumoResponse = Schemas['LeadResumoResponse']
export type ListaLeadsResponse = Schemas['ListaLeadsResponse']
export type LeadResponse = Schemas['LeadResponse']
export type AvisoLead = Schemas['AvisoLead']
export type CriarLeadRequest = Schemas['CriarLeadRequest']
export type CriarLeadResponse = Schemas['CriarLeadResponse']
export type EditarLeadRequest = Schemas['EditarLeadRequest']
export type FiltrosLeads = NonNullable<paths['/api/leads']['get']['parameters']['query']>
export type EtapaResumo = Schemas['EtapaResumo']
export type MotivoPerdaResponse = Schemas['MotivoPerdaResponse']
export type MudarEtapaRequest = Schemas['MudarEtapaRequest']
export type TipoInteracao = Schemas['TipoInteracao']
export type RegistrarInteracaoRequest = Schemas['RegistrarInteracaoRequest']
export type ItemLinhaDoTempo = Schemas['ItemLinhaDoTempo']
export type QuadroFunilResponse = Schemas['QuadroFunilResponse']
export type ColunaFunilResponse = Schemas['ColunaFunilResponse']
export type CartaoFunilResponse = Schemas['CartaoFunilResponse']
export type FiltrosFunil = NonNullable<paths['/api/funil']['get']['parameters']['query']>
export type ConfiguracoesResponse = Schemas['ConfiguracoesResponse']
export type EditarConfiguracoesRequest = Schemas['EditarConfiguracoesRequest']
export type TrocarResponsavelRequest = Schemas['TrocarResponsavelRequest']
export type TipoNotificacao = Schemas['TipoNotificacao']
export type NotificacaoResponse = Schemas['NotificacaoResponse']
export type NotificacoesResponse = Schemas['NotificacoesResponse']
export type CampoImportacao = Schemas['CampoImportacao']
export type Codificacao = Schemas['Codificacao']
export type Separador = Schemas['Separador']
export type AnalisarImportacaoRequest = Schemas['AnalisarImportacaoRequest']
export type AnaliseImportacaoResponse = Schemas['AnaliseImportacaoResponse']
export type CriarImportacaoRequest = Schemas['CriarImportacaoRequest']
export type ImportacaoResponse = Schemas['ImportacaoResponse']
export type PainelResponse = Schemas['PainelResponse']
export type FiltrosPainel = NonNullable<paths['/api/painel']['get']['parameters']['query']>

/** ProblemDetails da API. Nas respostas 400, `errors` traz as mensagens de cada campo. */
export type Problema = Schemas['ProblemDetails'] & { errors?: Record<string, string[]> }

/**
 * Cliente tipado da API, gerado a partir do OpenAPI (npm run api:tipos). A sessão vai no cookie, que o
 * navegador envia sozinho porque a API está na mesma origem (em desenvolvimento, pelo proxy do Vite).
 */
export const api = createClient<paths>({
  baseUrl: window.location.origin,
  // Busca o fetch global a cada chamada, para os testes (MSW) conseguirem interceptar.
  fetch: (requisicao) => globalThis.fetch(requisicao),
})

/** Falha numa chamada à API, com o ProblemDetails da resposta. */
export class ErroApi extends Error {
  readonly status: number
  readonly problema: Problema

  constructor(status: number, corpo: unknown) {
    const problema: Problema = typeof corpo === 'object' && corpo !== null ? corpo : {}
    super(problema.detail ?? problema.title ?? `A API respondeu com o erro ${status}.`)
    this.name = 'ErroApi'
    this.status = status
    this.problema = problema
  }

  /** Mensagens de validação por campo, com os nomes em camelCase, como no JSON. */
  get errosDosCampos(): Record<string, string[]> {
    return this.problema.errors ?? {}
  }
}

/** Devolve os dados de uma chamada do cliente ou lança {@link ErroApi} se a resposta não for 2xx. */
export async function exigirSucesso<T>(
  chamada: Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<T> {
  const { data, error, response } = await chamada
  if (!response.ok) {
    throw new ErroApi(response.status, error)
  }
  return data as T
}
