import { http, HttpResponse } from 'msw'
import type {
  CampanhaResponse,
  CartaoFunilResponse,
  EtapaResponse,
  ItemLinhaDoTempo,
  LeadResponse,
  LeadResumoResponse,
  MotivoPerdaResponse,
  QuadroFunilResponse,
  UsuarioResponse,
} from '@/api/cliente'
import { admin, atendente } from './api'
import { servidor } from './servidor'

export const etapas: EtapaResponse[] = [
  { id: 1, nome: 'Novo', ordem: 1, tipo: 'aberta', ativa: true },
  { id: 2, nome: 'Em contato', ordem: 2, tipo: 'aberta', ativa: true },
  { id: 3, nome: 'Qualificado', ordem: 3, tipo: 'aberta', ativa: true },
  { id: 4, nome: 'Proposta enviada', ordem: 4, tipo: 'aberta', ativa: true },
  { id: 5, nome: 'Ganho', ordem: 5, tipo: 'ganho', ativa: true },
  { id: 6, nome: 'Perdido', ordem: 6, tipo: 'perdido', ativa: true },
]

export const motivos: MotivoPerdaResponse[] = [
  { id: 1, nome: 'Sem resposta', ativo: true },
  { id: 2, nome: 'Sem interesse', ativo: true },
  { id: 3, nome: 'Preço', ativo: true },
  { id: 7, nome: 'Motivo antigo', ativo: false },
]

export const campanhas: CampanhaResponse[] = [
  {
    id: 10,
    origem: 'meta_lead_ads',
    idExterno: '120210000000001',
    nome: 'Outubro | Serviço Y',
    nomeProvisorio: false,
  },
]

function usuario(eu: typeof admin, parcial: Partial<UsuarioResponse> = {}): UsuarioResponse {
  return {
    id: eu.id,
    nome: eu.nome,
    email: eu.email,
    perfil: eu.perfil,
    ativo: true,
    participaRodizio: eu.perfil === 'Atendente',
    trocarSenha: false,
    bloqueadoAte: null,
    ultimoLeadAtribuidoEm: null,
    proximoDaVez: false,
    criadoEm: '2026-09-28T12:00:00Z',
    ...parcial,
  }
}

export const usuarios: UsuarioResponse[] = [
  usuario(atendente),
  usuario(admin),
  usuario(
    { ...atendente, id: '5b1e0000-0000-0000-0000-000000000003', nome: 'Pietra' },
    { ativo: false },
  ),
]

export function leadResumo(parcial: Partial<LeadResumoResponse> = {}): LeadResumoResponse {
  return {
    id: 1024,
    nome: 'Maria Souza',
    empresa: 'Empresa X',
    email: 'maria@empresax.com.br',
    telefone: '+5551999998888',
    etapa: { id: 1, nome: 'Novo', tipo: 'aberta' },
    origem: 'meta_lead_ads',
    campanha: { id: 10, nome: 'Outubro | Serviço Y' },
    responsavel: { id: atendente.id, nome: 'Adriana' },
    teste: false,
    criadoEm: '2026-10-05T17:03:00Z',
    ...parcial,
  }
}

export function fichaLead(parcial: Partial<LeadResponse> = {}): LeadResponse {
  const {
    id,
    nome,
    empresa,
    email,
    telefone,
    etapa,
    origem,
    campanha,
    responsavel,
    teste,
    criadoEm,
  } = leadResumo()
  return {
    id,
    nome,
    empresa,
    email,
    telefone,
    etapa,
    origem,
    campanha,
    responsavel,
    teste,
    criadoEm,
    cpfCnpj: null,
    uf: 'RS',
    plataforma: 'instagram',
    utmSource: 'instagram',
    utmMedium: 'paid',
    utmCampaign: 'outubro-servico-y',
    utmContent: null,
    utmTerm: null,
    gclid: null,
    respostasExtras: [{ pergunta: 'Qual serviço você procura?', resposta: 'Serviço Y' }],
    atualizadoEm: criadoEm,
    primeiroContatoEm: null,
    convertidoEm: null,
    perdidoEm: null,
    motivoPerda: null,
    motivoPerdaDetalhe: null,
    anonimizadoEm: null,
    avisos: [{ campo: 'cpfCnpj', mensagem: 'Sem CPF/CNPJ' }],
    versao: 777,
    ...parcial,
  }
}

/** Um item da linha do tempo; só o objeto do tipo vem preenchido. */
export function itemLinhaDoTempo(
  parcial: Partial<ItemLinhaDoTempo> & Pick<ItemLinhaDoTempo, 'tipo'>,
): ItemLinhaDoTempo {
  return {
    ocorridoEm: '2026-10-05T17:03:00Z',
    usuario: null,
    entrada: null,
    interacao: null,
    etapa: null,
    responsavel: null,
    ...parcial,
  }
}

export function cartao(parcial: Partial<CartaoFunilResponse> = {}): CartaoFunilResponse {
  return {
    id: 1024,
    nome: 'Maria Souza',
    empresa: 'Empresa X',
    origem: 'meta_lead_ads',
    responsavel: { id: atendente.id, nome: 'Adriana' },
    teste: false,
    anonimizado: false,
    criadoEm: '2026-10-05T17:03:00Z',
    ...parcial,
  }
}

/** Quadro com Maria em Novo e João em Em contato; Ganho e Perdido com os contadores dos 30 dias. */
export function quadroFunil(): QuadroFunilResponse {
  const coluna = (id: number, leads: CartaoFunilResponse[]) => {
    const etapa = etapas.find((e) => e.id === id)!
    return { etapa: { id, nome: etapa.nome, tipo: etapa.tipo }, total: leads.length, leads }
  }
  return {
    colunas: [
      coluna(1, [cartao()]),
      coluna(2, [cartao({ id: 1025, nome: 'João Lima', empresa: null, origem: 'google_ads' })]),
      coluna(3, []),
      coluna(4, []),
    ],
    ganho: { etapa: { id: 5, nome: 'Ganho', tipo: 'ganho' }, periodo: 3 },
    perdido: { etapa: { id: 6, nome: 'Perdido', tipo: 'perdido' }, periodo: 1 },
    desde: '2026-09-06',
  }
}

/** Quadro simulado, com os cadastros. Devolve os endereços pedidos, para conferir os filtros. */
export function comQuadro(quadro: QuadroFunilResponse = quadroFunil()) {
  const pedidos: URL[] = []
  comCadastros()
  servidor.use(
    http.get('*/api/funil', ({ request }) => {
      pedidos.push(new URL(request.url))
      return HttpResponse.json(quadro)
    }),
  )
  return pedidos
}

/** Etapas, motivos de perda, campanhas e usuários, que a lista, a ficha e os formulários usam. */
export function comCadastros() {
  servidor.use(
    http.get('*/api/etapas', () => HttpResponse.json(etapas)),
    http.get('*/api/motivos-perda', () => HttpResponse.json(motivos)),
    http.get('*/api/campanhas', () => HttpResponse.json(campanhas)),
    http.get('*/api/usuarios', () => HttpResponse.json(usuarios)),
  )
}

/** Lista simulada. Devolve os endereços pedidos, para conferir os filtros enviados. */
export function comLista(itens: LeadResumoResponse[], total = itens.length) {
  const pedidos: URL[] = []
  servidor.use(
    http.get('*/api/leads', ({ request }) => {
      const url = new URL(request.url)
      pedidos.push(url)
      return HttpResponse.json({
        itens,
        total,
        pagina: Number(url.searchParams.get('pagina') ?? 1),
        tamanho: 25,
      })
    }),
  )
  return pedidos
}
