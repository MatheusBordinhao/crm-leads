import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'

// O getElementError do setup.ts: a página só é formatada no erro que alguém vai ler. Se uma versão nova da Testing
// Library mudar a marca das tentativas (_disableExpensiveErrorDiagnostics), o segundo teste falha.
describe('o erro de uma consulta que falha', () => {
  it('fora de uma espera, mostra a página', () => {
    render(<p>Conteúdo da página</p>)

    expect(() => screen.getByText('Não existe')).toThrow(/Conteúdo da página/)
  })

  it('numa tentativa de dentro do waitFor, sai sem formatar a página', async () => {
    render(<p>Conteúdo da página</p>)
    let mensagem = ''

    await waitFor(() => {
      try {
        screen.getByText('Não existe')
      } catch (erro) {
        mensagem = (erro as Error).message
      }
    })

    expect(mensagem).toContain('Unable to find an element with the text: Não existe')
    expect(mensagem).not.toContain('Conteúdo da página')
  })

  it('no fim de uma espera que estoura o tempo, mostra a página', async () => {
    render(<p>Conteúdo da página</p>)

    await expect(screen.findByText('Não existe', undefined, { timeout: 100 })).rejects.toThrow(
      /Conteúdo da página/,
    )
  })
})
