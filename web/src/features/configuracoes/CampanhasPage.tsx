import { PencilIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { CampanhaResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useCampanhas } from '@/features/leads/dados'
import { ROTULOS_ORIGEM } from '@/features/leads/rotulos'
import { useRenomearCampanha } from './dados'
import { RenomearDialog } from './RenomearDialog'

const TAMANHO_MAXIMO_CAMPANHA = 200

/** As provisórias primeiro, porque são as que pedem um nome. Dentro de cada grupo, a ordem da API, pelo nome. */
function emOrdem(campanhas: CampanhaResponse[]) {
  return [
    ...campanhas.filter((campanha) => campanha.nomeProvisorio),
    ...campanhas.filter((campanha) => !campanha.nomeProvisorio),
  ]
}

/**
 * Campanhas (admin, Passo 6). O Google manda só o id da campanha, que chega com um nome provisório; aqui o admin dá o
 * nome que a Gama usa. O nome dado pelo admin não é substituído por um que chegue depois.
 */
export function CampanhasPage() {
  const { data: campanhas, isPending } = useCampanhas()
  const renomear = useRenomearCampanha()
  const [renomeando, setRenomeando] = useState<CampanhaResponse | null>(null)

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-xl font-semibold">Campanhas</h1>
      <Card>
        <CardHeader>
          <CardTitle>Campanhas dos anúncios</CardTitle>
          <CardDescription>
            O Google manda só o número da campanha, e ela chega com um nome provisório, como
            &quot;Google Ads · campanha 123&quot;. Dê a ela o nome que a Gama usa: ele aparece no
            painel, no relatório e nas fichas, e um nome que chegue depois não o substitui.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {isPending || !campanhas ? (
            <Carregando />
          ) : campanhas.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              Nenhuma campanha ainda. Cada uma aparece aqui quando chega o primeiro lead dela.
            </p>
          ) : (
            <ul aria-label="Campanhas" className="flex flex-col divide-y rounded-lg border">
              {emOrdem(campanhas).map((campanha) => (
                <li key={campanha.id} className="flex items-center gap-2 px-3 py-2">
                  <div className="flex min-w-0 flex-1 flex-col">
                    <span className="break-words">{campanha.nome}</span>
                    <span className="text-xs break-all text-muted-foreground">
                      {ROTULOS_ORIGEM[campanha.origem]} · id {campanha.idExterno}
                    </span>
                  </div>
                  {campanha.nomeProvisorio && <Badge variant="outline">Nome provisório</Badge>}
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Renomear ${campanha.nome}`}
                    onClick={() => setRenomeando(campanha)}
                  >
                    <PencilIcon />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      {renomeando && (
        <RenomearDialog
          titulo={`Renomear ${renomeando.nome}`}
          nomeAtual={renomeando.nome}
          tamanhoMaximo={TAMANHO_MAXIMO_CAMPANHA}
          aoSalvar={async (nome) => {
            await renomear.mutateAsync({ id: renomeando.id, nome })
            toast.success(`Campanha renomeada para ${nome}.`)
          }}
          aoFechar={() => setRenomeando(null)}
        />
      )}
    </section>
  )
}
