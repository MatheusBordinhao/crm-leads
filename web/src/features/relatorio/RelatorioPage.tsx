import { DownloadIcon } from 'lucide-react'
import { useSearchParams } from 'react-router'
import { ErroApi } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Button } from '@/components/ui/button'
import { DatasDoPeriodo, SelecaoPeriodo, type MudarEndereco } from '@/features/painel/FiltroPeriodo'
import {
  hojeEmBrasilia,
  intervaloDoAtalho,
  intervaloDoEndereco,
  lerPeriodo,
} from '@/features/painel/periodo'
import { formatarDia } from '@/lib/datas'
import { mensagemDoErro } from '@/lib/formularios'
import { cn } from '@/lib/utils'
import { linkDoCsv, useRelatorioCampanhas } from './dados'
import { TabelaRelatorio } from './TabelaRelatorio'

/**
 * Relatório por campanha (seção 8), só para o admin: o mesmo que a Gama recebe, sem dados pessoais. O período fica no
 * endereço, com os atalhos do painel, e o CSV sai com o período da tela.
 */
export function RelatorioPage() {
  const [params, setParams] = useSearchParams()
  const hoje = hojeEmBrasilia()
  const filtros = intervaloDoEndereco(params, hoje)
  const {
    data: relatorio,
    isPending,
    isError,
    error,
    refetch,
    isPlaceholderData,
  } = useRelatorioCampanhas(filtros)

  const mudar: MudarEndereco = (mudancas) => {
    const novos = new URLSearchParams(params)
    for (const [nome, valor] of Object.entries(mudancas)) {
      if (valor) {
        novos.set(nome, valor)
      } else {
        novos.delete(nome)
      }
    }
    setParams(novos, { replace: true })
  }

  return (
    <section className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold">Relatório por campanha</h1>
        {relatorio && (
          <p className="text-sm text-muted-foreground">
            {formatarDia(relatorio.de)} a {formatarDia(relatorio.ate)}
          </p>
        )}
      </div>
      <p className="text-sm text-muted-foreground">
        O relatório que a Gama recebe, sem dados pessoais. Leads novos são os criados no período,
        pela campanha da primeira entrada, e em andamento, ganhos e perdidos mostram onde eles estão
        hoje. As entradas contam também as de quem já era lead, e o primeiro contato é a mediana.
      </p>

      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="grid gap-3 sm:grid-cols-3 md:flex-1 lg:max-w-3xl">
          <SelecaoPeriodo
            id="relatorio-periodo"
            params={params}
            mudar={mudar}
            intervaloNaTela={
              relatorio ? { de: relatorio.de, ate: relatorio.ate } : intervaloDoAtalho('30d', hoje)
            }
          />
          {lerPeriodo(params) === 'personalizado' && (
            <DatasDoPeriodo
              prefixo="relatorio"
              params={params}
              mudar={mudar}
              className="sm:col-span-2"
            />
          )}
        </div>
        {relatorio && !isError && (
          <Button asChild variant="outline" className="self-start md:self-auto">
            <a href={linkDoCsv(filtros)} download>
              <DownloadIcon aria-hidden />
              Baixar CSV
            </a>
          </Button>
        )}
      </div>

      {isPending && <Carregando />}
      {isError && <Falha erro={error} tentarDeNovo={() => refetch()} />}
      {relatorio && !isError && (
        // Com um período novo carregando, os números anteriores ficam na tela, mais claros.
        <div
          aria-busy={isPlaceholderData}
          className={cn('transition-opacity', isPlaceholderData && 'opacity-60')}
        >
          <TabelaRelatorio relatorio={relatorio} />
        </div>
      )}
    </section>
  )
}

/** Um período inválido mostra a mensagem da API; as outras falhas oferecem tentar de novo. */
function Falha({ erro, tentarDeNovo }: { erro: Error; tentarDeNovo: () => void }) {
  if (erro instanceof ErroApi && erro.status === 400) {
    return (
      <p role="alert" className="text-sm text-destructive">
        {mensagemDoErro(erro)}
      </p>
    )
  }
  return (
    <div className="flex flex-col items-start gap-2">
      <p>Não foi possível carregar o relatório.</p>
      <Button variant="outline" onClick={tentarDeNovo}>
        Tentar de novo
      </Button>
    </div>
  )
}
