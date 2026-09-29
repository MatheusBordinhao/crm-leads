import { http, HttpResponse } from 'msw'
import type {
  CampanhaResponse,
  EtapaResponse,
  LeadResponse,
  LeadResumoResponse,
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
    anonimizadoEm: null,
    avisos: [{ campo: 'cpfCnpj', mensagem: 'Sem CPF/CNPJ' }],
    versao: 777,
    ...parcial,
  }
}

/** Etapas, campanhas e usuários, que a lista e os formulários usam. */
export function comCadastros() {
  servidor.use(
    http.get('*/api/etapas', () => HttpResponse.json(etapas)),
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
