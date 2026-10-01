import { screen, waitFor, within } from '@testing-library/react'
import { http } from 'msw'
import { describe, expect, it } from 'vitest'
import { admin, atendente, comSessao, problema } from '@/test/api'
import { comImportacao, importacao } from '@/test/importacao'
import { comCadastros, comLista, comQuadro } from '@/test/leads'
import { renderizarApp } from '@/test/renderizar'
import { servidor } from '@/test/servidor'
import { MENSAGEM_TAMANHO } from './arquivo'

const arquivoDaMeta = () => new File(['id\tfull_name'], 'leads-meta.csv', { type: 'text/csv' })

/** Abre a importação como admin e escolhe o arquivo. */
async function escolherArquivo(arquivo = arquivoDaMeta()) {
  const app = renderizarApp('/importacao')
  await app.usuario.upload(await screen.findByLabelText('Arquivo CSV'), arquivo)
  return app
}

describe('importação', () => {
  it('lê o arquivo escolhido e mostra a codificação, a prévia e o campo sugerido para cada coluna', async () => {
    comSessao(admin)
    comCadastros()
    const { analisados } = comImportacao()

    await escolherArquivo()

    expect(await screen.findByText('UTF-16 · separado por tab · 2 linhas')).toBeInTheDocument()
    const previa = within(screen.getByRole('table', { name: 'Prévia do arquivo' }))
    expect(previa.getByRole('columnheader', { name: 'full_name' })).toBeInTheDocument()
    expect(previa.getByText('João Conceição')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'full_name' })).toHaveValue('nome')
    expect(screen.getByRole('combobox', { name: 'qual_serviço_você_procura?' })).toHaveValue(
      'resposta_extra',
    )
    expect(screen.getByText('Exemplo: João Conceição')).toBeInTheDocument()
    expect(screen.getByRole('combobox', { name: 'Origem' })).toHaveValue('meta_lead_ads')
    expect(screen.getByRole('combobox', { name: 'Coluna de id externo' })).toHaveValue('0')
    expect(screen.getByRole('combobox', { name: 'Distribuição' })).toHaveValue('')
    expect(analisados).toEqual([
      { nomeArquivo: 'leads-meta.csv', conteudoBase64: btoa('id\tfull_name') },
    ])
  })

  it('importa com o campo de cada coluna e as opções escolhidas e abre o resultado', async () => {
    comSessao(admin)
    comCadastros()
    const { criados } = comImportacao()
    const { usuario, router } = await escolherArquivo()

    await usuario.selectOptions(
      await screen.findByRole('combobox', { name: 'qual_serviço_você_procura?' }),
      'nao_importar',
    )
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Origem' }), 'planilha')
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Coluna de id externo' }), '')
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Campanha padrão' }), 'nova')
    await usuario.type(screen.getByLabelText('Nome da campanha nova'), '  Feira de Gramado ')
    const distribuicao = screen.getByRole('combobox', { name: 'Distribuição' })
    await within(distribuicao).findByRole('option', { name: 'Adriana' })
    await usuario.selectOptions(distribuicao, atendente.id)
    await usuario.click(screen.getByRole('button', { name: 'Importar 2 linhas' }))

    expect(await screen.findByRole('heading', { name: 'leads-meta.csv' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/importacao/12')
    expect(criados).toEqual([
      {
        nomeArquivo: 'leads-meta.csv',
        conteudoBase64: btoa('id\tfull_name'),
        campos: ['nao_importar', 'nome', 'email', 'telefone', 'nao_importar'],
        colunaIdExterno: null,
        origem: 'planilha',
        campanhaPadraoId: null,
        novaCampanha: 'Feira de Gramado',
        responsavelId: atendente.id,
      },
    ])
  })

  it('as campanhas que já existem são as da origem escolhida', async () => {
    comSessao(admin)
    comCadastros()
    const { criados } = comImportacao()
    const { usuario } = await escolherArquivo()

    const campanha = await screen.findByRole('combobox', { name: 'Campanha padrão' })
    await within(campanha).findByRole('option', { name: 'Outubro | Serviço Y' })
    await usuario.selectOptions(campanha, 'Outubro | Serviço Y')
    await usuario.click(screen.getByRole('button', { name: 'Importar 2 linhas' }))
    await screen.findByRole('heading', { name: 'leads-meta.csv' })

    expect(criados[0]).toMatchObject({ campanhaPadraoId: 10, novaCampanha: null })
  })

  it('a campanha de outra origem some quando a origem muda', async () => {
    comSessao(admin)
    comCadastros()
    comImportacao()
    const { usuario } = await escolherArquivo()

    const campanha = await screen.findByRole('combobox', { name: 'Campanha padrão' })
    await within(campanha).findByRole('option', { name: 'Outubro | Serviço Y' })
    await usuario.selectOptions(campanha, 'Outubro | Serviço Y')
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'Origem' }), 'planilha')

    expect(campanha).toHaveValue('')
    expect(within(campanha).queryByRole('option', { name: 'Outubro | Serviço Y' })).toBeNull()
  })

  it('confere o mapeamento antes de chamar a API', async () => {
    comSessao(admin)
    comCadastros()
    const { criados } = comImportacao()
    const { usuario } = await escolherArquivo()

    await usuario.selectOptions(
      await screen.findByRole('combobox', { name: 'full_name' }),
      'nao_importar',
    )
    await usuario.selectOptions(screen.getByRole('combobox', { name: 'email' }), 'telefone')
    await usuario.click(screen.getByRole('button', { name: 'Importar 2 linhas' }))

    expect(await screen.findByText('Escolha a coluna do nome.')).toBeInTheDocument()
    expect(screen.getByText('Telefone já está em outra coluna.')).toBeInTheDocument()
    expect(criados).toEqual([])
  })

  it('pede o nome da campanha nova', async () => {
    comSessao(admin)
    comCadastros()
    const { criados } = comImportacao()
    const { usuario } = await escolherArquivo()

    await usuario.selectOptions(
      await screen.findByRole('combobox', { name: 'Campanha padrão' }),
      'nova',
    )
    await usuario.click(screen.getByRole('button', { name: 'Importar 2 linhas' }))

    expect(await screen.findByText('Dê o nome da campanha nova.')).toBeInTheDocument()
    expect(criados).toEqual([])
  })

  it('arquivo maior que 5 MB é recusado sem chamar a API', async () => {
    comSessao(admin)
    comCadastros()
    const { analisados } = comImportacao()

    await escolherArquivo(new File([new Uint8Array(5 * 1024 * 1024 + 1)], 'grande.csv'))

    expect(await screen.findByText(MENSAGEM_TAMANHO)).toBeInTheDocument()
    expect(analisados).toEqual([])
  })

  it('mostra o motivo quando a API recusa o arquivo', async () => {
    comSessao(admin)
    comCadastros()
    const mensagem = 'Esse arquivo é uma planilha do Excel, e não um CSV.'
    servidor.use(
      http.post('*/api/importacoes/analise', () =>
        problema(400, { errors: { arquivo: [mensagem] } }),
      ),
    )

    await escolherArquivo(new File(['PK'], 'leads.csv'))

    expect(await screen.findByText(mensagem)).toBeInTheDocument()
    expect(screen.queryByRole('table', { name: 'Prévia do arquivo' })).not.toBeInTheDocument()
  })

  it('mostra o andamento enquanto processa e o resultado quando termina', async () => {
    comSessao(admin)
    comImportacao({
      consultas: [
        importacao({
          situacao: 'processando',
          linhasProcessadas: 2,
          criados: 1,
          concluidoEm: null,
        }),
        importacao(),
      ],
    })

    renderizarApp('/importacao/12')

    const andamento = await screen.findByRole('progressbar', { name: 'Andamento da importação' })
    expect(andamento).toHaveAttribute('aria-valuenow', '2')
    expect(andamento).toHaveAttribute('aria-valuemax', '5')
    expect(screen.getByText('2 de 5 linhas')).toBeInTheDocument()
    expect(await screen.findByText('Leads novos', {}, { timeout: 3000 })).toBeInTheDocument()
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it('o resultado mostra os números, os sem responsável e as linhas com erro com o relatório', async () => {
    comSessao(admin)
    const erros = Array.from({ length: 100 }, (_, i) => ({
      linha: i + 2,
      motivo: 'A linha não tem o nome.',
    }))
    comImportacao({ consultas: [importacao({ semResponsavel: 2, comErro: 101, erros })] })

    renderizarApp('/importacao/12')

    const numero = async (rotulo: string) =>
      (await screen.findByText(rotulo, { selector: 'dt' })).nextElementSibling?.textContent
    expect(await numero('Leads novos')).toBe('3')
    expect(await numero('Vinculados a leads existentes')).toBe('1')
    expect(await numero('Já importados antes')).toBe('0')
    expect(await numero('Com erro')).toBe('101')
    expect(screen.getByText('2 leads novos ficaram sem responsável.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver os leads sem responsável' })).toHaveAttribute(
      'href',
      '/leads?responsavel=sem',
    )
    const tabela = within(screen.getByRole('table', { name: 'Linhas com erro' }))
    expect(tabela.getAllByRole('cell', { name: 'A linha não tem o nome.' })).toHaveLength(100)
    expect(
      screen.getByText(/Aqui estão as 100 primeiras; o relatório traz todas/),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Baixar relatório de erros' })).toHaveAttribute(
      'href',
      '/api/importacoes/12/erros',
    )
  })

  it('importação que não existe', async () => {
    comSessao(admin)
    servidor.use(http.get('*/api/importacoes/:id', () => problema(404)))

    renderizarApp('/importacao/999')

    expect(
      await screen.findByRole('heading', { name: 'Importação não encontrada' }),
    ).toBeInTheDocument()
  })

  it('o admin abre a importação pelo menu', async () => {
    comSessao(admin)
    comQuadro()
    const { usuario } = renderizarApp('/')

    await usuario.click(await screen.findByRole('link', { name: 'Importação' }))

    expect(await screen.findByRole('heading', { name: 'Importação de leads' })).toBeInTheDocument()
  })

  it('a atendente não vê a importação no menu nem abre a tela', async () => {
    comSessao(atendente)
    comQuadro()
    comLista([])
    const { router } = renderizarApp('/importacao')

    await waitFor(() => expect(router.state.location.pathname).toBe('/funil'))
    expect(await screen.findByRole('link', { name: 'Funil' })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Importação' })).not.toBeInTheDocument()
  })
})

describe('resultado sem erros', () => {
  it('não mostra a tabela nem o relatório de erros', async () => {
    comSessao(admin)
    comImportacao({ consultas: [importacao({ comErro: 0, erros: [] })] })

    renderizarApp('/importacao/12')

    expect(await screen.findByText('Leads novos')).toBeInTheDocument()
    expect(screen.queryByRole('table', { name: 'Linhas com erro' })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Baixar relatório de erros' }),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Ver os leads' })).toHaveAttribute('href', '/leads')
  })
})
