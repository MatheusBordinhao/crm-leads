import { describe, expect, it } from 'vitest'
import { descreverDetalhes, descreverEntidade, rotuloAcao, type Nomes } from './rotulos'

const nomes: Nomes = {
  usuario: (id) => ({ a: 'Adriana', p: 'Pietra' })[id],
  etapa: (id) => ({ 1: 'Novo', 2: 'Em contato' })[id],
  motivo: (id) => ({ 3: 'Preço' })[id],
  campanha: (id) => ({ 10: 'Outubro | Serviço Y' })[id],
}

describe('rótulos da auditoria', () => {
  it('cada ação tem um rótulo, e uma ação desconhecida aparece como veio', () => {
    expect(rotuloAcao('lead.anonimizado')).toBe('Lead anonimizado')
    expect(rotuloAcao('leads.exportados')).toBe('Leads exportados em CSV')
    expect(rotuloAcao('acao.nova')).toBe('acao.nova')
  })

  it('o registro afetado aparece pelo número, ou pelo nome quando não é um lead', () => {
    expect(descreverEntidade('lead', '1024', nomes)).toBe('Lead 1024')
    expect(descreverEntidade('usuario', 'a', nomes)).toBe('Adriana')
    expect(descreverEntidade('etapa', '2', nomes)).toBe('Etapa Em contato')
    expect(descreverEntidade('etapa', null, nomes)).toBe('Etapas')
    expect(descreverEntidade('motivo_perda', '3', nomes)).toBe('Motivo de perda Preço')
    expect(descreverEntidade('campanha', '10', nomes)).toBe('Campanha Outubro | Serviço Y')
    expect(descreverEntidade('campanha', '99', nomes)).toBe('Campanha 99')
    expect(descreverEntidade('configuracao', 'distribuicao_automatica', nomes)).toBe(
      'Distribuição automática (rodízio)',
    )
  })

  it('os detalhes viram texto, com os nomes que a tela conhece', () => {
    expect(descreverDetalhes('login.falha', { motivo: 'senha_incorreta' }, nomes)).toBe(
      'Senha incorreta',
    )
    expect(descreverDetalhes('lead.editado', { campos: ['nome', 'email', 'cpfCnpj'] }, nomes)).toBe(
      'Campos: nome, e-mail, CPF/CNPJ',
    )
    expect(descreverDetalhes('lead.reatribuido', { anteriorId: null, novoId: 'p' }, nomes)).toBe(
      'De ninguém para Pietra',
    )
    expect(descreverDetalhes('lead.criado', { origem: 'meta_lead_ads' }, nomes)).toBe(
      'Origem: Meta (Facebook e Instagram)',
    )
    expect(descreverDetalhes('etapa.desativada', { leadsMovidos: 3, destinoId: 2 }, nomes)).toBe(
      '3 leads movidos para Em contato',
    )
    expect(descreverDetalhes('configuracao.alterada', { valor: false }, nomes)).toBe('Desligada')
    expect(rotuloAcao('campanha.renomeada')).toBe('Campanha renomeada')
    expect(descreverDetalhes('campanha.renomeada', { campos: ['nome'] }, nomes)).toBe(
      'Campos: nome',
    )
    expect(
      descreverDetalhes('lead.anonimizado', { entradas: 1, interacoes: 2, eventos: 3 }, nomes),
    ).toBe('1 entrada e 2 interações limpas; 3 avisos ao n8n apagados')
  })

  it('a exportação mostra as linhas e os filtros, e da busca só que houve uma', () => {
    expect(
      descreverDetalhes(
        'leads.exportados',
        {
          linhas: 12,
          filtros: { busca: true, etapaId: 1, semResponsavel: true, de: '2026-10-01' },
        },
        nomes,
      ),
    ).toBe('12 leads, busca, etapa Novo, sem responsável, entrada a partir de 01/10/2026')
    expect(descreverDetalhes('leads.exportados', { linhas: 1, filtros: {} }, nomes)).toBe(
      '1 lead, sem filtros',
    )
  })

  it('sem detalhes, ou com uma ação desconhecida, não inventa nada', () => {
    expect(descreverDetalhes('lead.dados_exportados', null, nomes)).toBeNull()
    expect(descreverDetalhes('acao.nova', { quantidade: 2, ligada: true }, nomes)).toBe(
      'quantidade: 2 · ligada: sim',
    )
  })
})
