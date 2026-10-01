/** Tamanho máximo do arquivo, o mesmo da API (seção 3.7). */
export const TAMANHO_MAXIMO = 5 * 1024 * 1024

export const MENSAGEM_TAMANHO = 'O arquivo passa de 5 MB. Divida-o em arquivos menores.'

/** O conteúdo do arquivo em base64, como a API recebe. */
export function lerEmBase64(arquivo: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const leitor = new FileReader()
    leitor.onload = () => {
      // O resultado é uma data URL: "data:text/csv;base64,<conteúdo>".
      const url = String(leitor.result)
      resolve(url.slice(url.indexOf(',') + 1))
    }
    leitor.onerror = () => reject(leitor.error ?? new Error('Não foi possível ler o arquivo.'))
    leitor.readAsDataURL(arquivo)
  })
}
