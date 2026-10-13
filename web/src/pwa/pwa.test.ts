import { describe, expect, it } from 'vitest'
import pagina from '../../index.html?raw'
import appleTouchIcon from '../../public/apple-touch-icon.png?inline'
import icone192 from '../../public/icone-192.png?inline'
import icone512 from '../../public/icone-512.png?inline'
import iconeMaskable from '../../public/icone-maskable-512.png?inline'
import iconeNotificacao from '../../public/icone-notificacao.png?inline'
import textoDoManifesto from '../../public/manifest.webmanifest?raw'

type Icone = { src: string; sizes: string; type: string; purpose: string }

const manifesto = JSON.parse(textoDoManifesto) as Record<string, unknown> & { icons: Icone[] }

/** Os PNGs de public/, gerados por icones/gerar.sh, pelo endereço em que o site os serve. */
const pngs: Record<string, string> = {
  '/icone-192.png': icone192,
  '/icone-512.png': icone512,
  '/icone-maskable-512.png': iconeMaskable,
  '/apple-touch-icon.png': appleTouchIcon,
  '/icone-notificacao.png': iconeNotificacao,
}

/** A largura e a altura de um PNG, do cabeçalho dele (IHDR, nos bytes 16 a 23). */
function tamanho(dataUrl: string) {
  const bytes = Uint8Array.from(atob(dataUrl.split(',')[1]), (c) => c.charCodeAt(0))
  const dados = new DataView(bytes.buffer)
  expect([...bytes.slice(1, 4)].map((b) => String.fromCharCode(b)).join('')).toBe('PNG')
  return `${dados.getUint32(16)}x${dados.getUint32(20)}`
}

describe('instalação como app (PWA)', () => {
  it('o manifesto tem o que o Chrome pede para instalar', () => {
    expect(manifesto).toMatchObject({
      name: 'CRM de Leads',
      short_name: 'CRM de Leads',
      start_url: '/',
      scope: '/',
      display: 'standalone',
      lang: 'pt-BR',
    })
    expect(manifesto).not.toHaveProperty('prefer_related_applications')
    expect(manifesto.icons).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ sizes: '192x192', purpose: 'any' }),
        expect.objectContaining({ sizes: '512x512', purpose: 'any' }),
        expect.objectContaining({ sizes: '512x512', purpose: 'maskable' }),
      ]),
    )
  })

  it('cada ícone do manifesto existe, com o tamanho declarado', () => {
    for (const icone of manifesto.icons) {
      expect(pngs[icone.src], icone.src).toBeDefined()
      expect(icone.type).toBe('image/png')
      expect(tamanho(pngs[icone.src]), icone.src).toBe(icone.sizes)
    }
    expect(tamanho(appleTouchIcon)).toBe('180x180')
    expect(tamanho(iconeNotificacao)).toBe('96x96')
  })

  it('a página aponta para o manifesto, o ícone do iPhone e a cor da barra', () => {
    expect(pagina).toContain('<link rel="manifest" href="/manifest.webmanifest" />')
    expect(pagina).toContain('<link rel="apple-touch-icon" href="/apple-touch-icon.png" />')
    expect(pagina).toContain(`<meta name="theme-color" content="${manifesto.theme_color}" />`)
  })
})
