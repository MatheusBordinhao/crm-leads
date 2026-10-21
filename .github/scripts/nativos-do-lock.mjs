// Compara o web/package-lock.json, entrada por entrada, antes de uma mudança chegar ao Windows (README › Versões fixadas ›
// Um pacote do npm com alerta): o que entrou, saiu ou mudou de versão, e se as entradas com binário nativo (as que têm
// os ou cpu, como o rolldown, o lightningcss e o oxide do Tailwind) ficaram iguais. O Controle Inteligente de
// Aplicativos do Windows bloqueia um binário nativo novo que não seja assinado.
// Sai com o código 1 se uma entrada nativa entrou ou mudou de versão. A que só sai não conta: não baixa nada.
//   node .github/scripts/nativos-do-lock.mjs                       # o do último commit contra o da pasta do projeto
//   node .github/scripts/nativos-do-lock.mjs <antes.json> <depois.json>
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const raiz = join(dirname(fileURLToPath(import.meta.url)), '..', '..')
const argumentos = process.argv.slice(2)
if (argumentos.length !== 0 && argumentos.length !== 2) {
  console.error('Uso: node .github/scripts/nativos-do-lock.mjs [<antes.json> <depois.json>]')
  process.exit(2)
}

const [textoAntes, textoDepois] =
  argumentos.length === 2
    ? argumentos.map((arquivo) => readFileSync(arquivo, 'utf8'))
    : [
        execFileSync('git', ['show', 'HEAD:web/package-lock.json'], {
          cwd: raiz,
          encoding: 'utf8',
          maxBuffer: 64 * 1024 * 1024,
        }),
        readFileSync(join(raiz, 'web', 'package-lock.json'), 'utf8'),
      ]
const antes = JSON.parse(textoAntes).packages
const depois = JSON.parse(textoDepois).packages

const nativa = (info) => Boolean(info?.os || info?.cpu)
const marcas = (info) =>
  [
    info.inBundle && 'inBundle',
    info.optional && 'opcional',
    info.dev && 'dev',
    nativa(info) && 'nativa',
  ]
    .filter(Boolean)
    .join(', ')

const caminhos = [...new Set([...Object.keys(antes), ...Object.keys(depois)])]
  .filter((c) => c !== '')
  .sort()
const entraram = caminhos.filter((c) => !(c in antes))
const sairam = caminhos.filter((c) => !(c in depois))
const mudaram = caminhos.filter(
  (c) => c in antes && c in depois && antes[c].version !== depois[c].version,
)
const soMarcas = caminhos.filter(
  (c) =>
    c in antes &&
    c in depois &&
    antes[c].version === depois[c].version &&
    JSON.stringify(antes[c]) !== JSON.stringify(depois[c]),
)

console.log(
  `entraram ${entraram.length}, saíram ${sairam.length}, mudaram de versão ${mudaram.length}, ` +
    `mudaram só em outro campo ${soMarcas.length}`,
)
for (const c of entraram) console.log(`  + ${c} ${depois[c].version} (${marcas(depois[c])})`)
for (const c of sairam) console.log(`  - ${c} ${antes[c].version} (${marcas(antes[c])})`)
for (const c of mudaram)
  console.log(`  ~ ${c} ${antes[c].version} -> ${depois[c].version} (${marcas(depois[c])})`)

const nativasNovas = entraram.filter((c) => nativa(depois[c]))
const nativasMudadas = mudaram.filter((c) => nativa(antes[c]) || nativa(depois[c]))
const nativasSairam = sairam.filter((c) => nativa(antes[c]))
const nativasAntes = caminhos.filter((c) => nativa(antes[c])).length
if (nativasNovas.length === 0 && nativasMudadas.length === 0) {
  console.log(
    `entradas com binário nativo: IGUAIS (${nativasAntes - nativasSairam.length} ficaram, ${nativasSairam.length} saíram)`,
  )
} else {
  console.log(
    `entradas com binário nativo: DIFERENTES (${nativasNovas.length} entraram, ${nativasMudadas.length} mudaram de versão)`,
  )
  process.exitCode = 1
}
