import type { AnaliseImportacaoResponse, CriarImportacaoRequest } from '@/api/cliente'
import { z } from '@/lib/zod'
import { CAMPOS, ORIGENS_DA_IMPORTACAO, ROTULOS_CAMPO, umaColunaSo } from './rotulos'

/** Valor da campanha padrão que pede o nome de uma campanha nova. */
export const NOVA_CAMPANHA = 'nova'

export const MENSAGENS = {
  nome: 'Escolha a coluna do nome.',
  contato: 'Escolha a coluna do e-mail ou a do telefone.',
  novaCampanha: 'Dê o nome da campanha nova.',
  novaCampanhaLonga: 'O nome da campanha pode ter até 100 caracteres.',
}

/** As escolhas da importação, conferidas como a API confere. */
export const esquema = z
  .object({
    /** O campo de cada coluna, na ordem do arquivo. */
    campos: z.array(z.enum(CAMPOS)),
    /** O índice da coluna de id externo; vazio quando não há. */
    colunaIdExterno: z.string(),
    origem: z.enum(ORIGENS_DA_IMPORTACAO),
    /** Vazio (nenhuma), {@link NOVA_CAMPANHA} ou o id de uma campanha que já existe. */
    campanha: z.string(),
    novaCampanha: z.string(),
    /** Vazio é o rodízio. */
    responsavelId: z.string(),
  })
  .superRefine((dados, contexto) => {
    const erro = (caminho: (string | number)[], mensagem: string) =>
      contexto.addIssue({ code: 'custom', path: caminho, message: mensagem })

    // O que vale para o mapeamento todo fica fora de "campos", que guarda o erro de cada coluna.
    if (!dados.campos.includes('nome')) {
      erro(['root', 'mapeamento'], MENSAGENS.nome)
    } else if (!dados.campos.includes('email') && !dados.campos.includes('telefone')) {
      erro(['root', 'mapeamento'], MENSAGENS.contato)
    }
    dados.campos.forEach((campo, coluna) => {
      if (umaColunaSo(campo) && dados.campos.indexOf(campo) !== coluna) {
        erro(['campos', coluna], `${ROTULOS_CAMPO[campo]} já está em outra coluna.`)
      }
    })
    const novaCampanha = dados.novaCampanha.trim()
    if (dados.campanha === NOVA_CAMPANHA && !novaCampanha) {
      erro(['novaCampanha'], MENSAGENS.novaCampanha)
    } else if (dados.campanha === NOVA_CAMPANHA && novaCampanha.length > 100) {
      erro(['novaCampanha'], MENSAGENS.novaCampanhaLonga)
    }
  })

export type EscolhasImportacao = z.infer<typeof esquema>

/** O que a análise sugeriu: o campo de cada coluna, a coluna de id e a origem. */
export function escolhasIniciais(analise: AnaliseImportacaoResponse): EscolhasImportacao {
  return {
    campos: analise.colunas.map((coluna) => coluna.sugestao),
    colunaIdExterno: analise.colunaIdExterno === null ? '' : String(analise.colunaIdExterno),
    origem: analise.origem === 'manual' ? 'planilha' : analise.origem,
    campanha: '',
    novaCampanha: '',
    responsavelId: '',
  }
}

export function paraApi(
  escolhas: EscolhasImportacao,
  arquivo: { nome: string; conteudoBase64: string },
): CriarImportacaoRequest {
  const novaCampanha = escolhas.campanha === NOVA_CAMPANHA
  return {
    nomeArquivo: arquivo.nome,
    conteudoBase64: arquivo.conteudoBase64,
    campos: escolhas.campos,
    colunaIdExterno: escolhas.colunaIdExterno === '' ? null : Number(escolhas.colunaIdExterno),
    origem: escolhas.origem,
    campanhaPadraoId: escolhas.campanha && !novaCampanha ? Number(escolhas.campanha) : null,
    novaCampanha: novaCampanha ? escolhas.novaCampanha.trim() : null,
    responsavelId: escolhas.responsavelId || null,
  }
}
