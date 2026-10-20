// O resumo de uma rodada da suíte do front, em Markdown, a partir do relatório JSON do Vitest: a máquina, o tempo da
// suíte, as falhas e os 10 testes mais lentos, com o "Perdido pede o motivo" em destaque. No GitHub, a saída vai para
// o resumo da rodada ($GITHUB_STEP_SUMMARY). Só lê o relatório: não precisa do node_modules.
//   node .github/scripts/resumo-do-front.mjs <relatório JSON do Vitest>
import { existsSync, readFileSync } from 'node:fs'
import { availableParallelism, cpus } from 'node:os'

// O teste com a espera de menos folga na medição de 20/10/2026 (CLAUDE.md).
const DESTAQUE = 'Perdido pede o motivo, e cancelar deixa o lead onde estava'

const arquivo = process.argv[2]
const numero = (n) => n.toLocaleString('pt-BR')
const celula = (texto) => texto.replaceAll('|', '\\|')

const linhas = [
  '### Suíte do front',
  '',
  `- Máquina: ${cpus()[0]?.model.trim() ?? 'desconhecida'}, com ${availableParallelism()} vCPUs`,
]

if (!arquivo || !existsSync(arquivo)) {
  linhas.push('- O Vitest não gravou o relatório: veja o log do passo da suíte.')
  console.log(linhas.join('\n'))
  process.exit(0)
}

const relatorio = JSON.parse(readFileSync(arquivo, 'utf8'))
const testes = relatorio.testResults.flatMap((deUmArquivo) =>
  deUmArquivo.assertionResults.map((teste) => ({
    nome: `${deUmArquivo.name.split(/[\\/]/).pop()} › ${teste.title}`,
    titulo: teste.title,
    ms: Math.round(teste.duration ?? 0),
    falhou: teste.status === 'failed',
  })),
)
const fim = Math.max(...relatorio.testResults.map((deUmArquivo) => deUmArquivo.endTime ?? 0))
const falharam = testes.filter((teste) => teste.falhou)
const destaque = testes.find((teste) => teste.titulo === DESTAQUE)

linhas.push(
  `- ${numero(testes.length)} testes em ${numero(Math.round((fim - relatorio.startTime) / 1000))} s: ` +
    (falharam.length === 0 ? 'nenhum falhou' : `**${numero(falharam.length)} falharam**`),
  `- Em destaque, "${DESTAQUE}": ${destaque ? `${numero(destaque.ms)} ms` : 'não rodou'}`,
)

if (falharam.length > 0) {
  linhas.push('', '**Falharam:**', '', ...falharam.map((teste) => `- ${teste.nome}`))
}

linhas.push('', '**Os 10 mais lentos:**', '', '| Teste | Duração |', '| --- | ---: |')
for (const teste of [...testes].sort((a, b) => b.ms - a.ms).slice(0, 10)) {
  linhas.push(`| ${celula(teste.nome)} | ${numero(teste.ms)} ms |`)
}

console.log(linhas.join('\n'))
