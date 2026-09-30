import { ArrowDownIcon, ArrowUpIcon, PencilIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { EtapaResponse, MotivoPerdaResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Switch } from '@/components/ui/switch'
import { useEtapas, useMotivosPerda } from '@/features/leads/dados'
import { SeloEtapa } from '@/features/leads/SeloEtapa'
import { mensagemDoErro } from '@/lib/formularios'
import {
  useConfiguracoes,
  useCriarEtapa,
  useCriarMotivo,
  useEditarConfiguracoes,
  useEditarMotivo,
  useReativarEtapa,
  useRenomearEtapa,
  useReordenarEtapas,
} from './dados'
import { DesativarEtapaDialog } from './DesativarEtapaDialog'
import { NovoNome } from './NovoNome'
import { RenomearDialog } from './RenomearDialog'

const TAMANHO_MAXIMO_ETAPA = 50
const TAMANHO_MAXIMO_MOTIVO = 100

/** Configuração do funil (admin): etapas, motivos de perda e a exigência de documento para Ganho. */
export function ConfiguracaoFunilPage() {
  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <h1 className="text-xl font-semibold">Etapas e motivos</h1>
      <Etapas />
      <MotivosDePerda />
      <Regras />
    </section>
  )
}

function Etapas() {
  const { data: etapas, isPending } = useEtapas()
  const criar = useCriarEtapa()
  const renomear = useRenomearEtapa()
  const reordenar = useReordenarEtapas()
  const reativar = useReativarEtapa()
  const [renomeando, setRenomeando] = useState<EtapaResponse | null>(null)
  const [desativando, setDesativando] = useState<EtapaResponse | null>(null)

  if (isPending || !etapas) {
    return <Carregando />
  }

  const abertas = etapas.filter((etapa) => etapa.tipo === 'aberta')
  const ativas = abertas.filter((etapa) => etapa.ativa)
  const fechamento = etapas.filter((etapa) => etapa.tipo !== 'aberta' && etapa.ativa)
  const falhar = (erro: Error) => toast.error(mensagemDoErro(erro))

  const mover = (indice: number, direcao: -1 | 1) => {
    const ids = abertas.map((etapa) => etapa.id)
    ;[ids[indice], ids[indice + direcao]] = [ids[indice + direcao], ids[indice]]
    reordenar.mutate(ids, { onError: falhar })
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Etapas</CardTitle>
        <CardDescription>
          A primeira etapa ativa é onde os leads novos entram. Ganho e Perdido ficam sempre no fim
          do funil.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ol aria-label="Etapas abertas" className="flex flex-col divide-y rounded-lg border">
          {abertas.map((etapa, indice) => (
            <li key={etapa.id} className="flex flex-wrap items-center gap-2 px-3 py-2">
              <span className="min-w-0 flex-1 break-words">{etapa.nome}</span>
              {!etapa.ativa && <Badge variant="outline">Desativada</Badge>}
              <div className="flex items-center gap-1">
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Subir ${etapa.nome}`}
                  disabled={indice === 0 || reordenar.isPending}
                  onClick={() => mover(indice, -1)}
                >
                  <ArrowUpIcon />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Descer ${etapa.nome}`}
                  disabled={indice === abertas.length - 1 || reordenar.isPending}
                  onClick={() => mover(indice, 1)}
                >
                  <ArrowDownIcon />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label={`Renomear ${etapa.nome}`}
                  onClick={() => setRenomeando(etapa)}
                >
                  <PencilIcon />
                </Button>
                {etapa.ativa ? (
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Desativar ${etapa.nome}`}
                    // Sempre sobra uma etapa aberta ativa: a etapa inicial.
                    disabled={ativas.length <= 1}
                    onClick={() => setDesativando(etapa)}
                  >
                    Desativar
                  </Button>
                ) : (
                  <Button
                    variant="outline"
                    size="sm"
                    aria-label={`Reativar ${etapa.nome}`}
                    disabled={reativar.isPending}
                    onClick={() =>
                      reativar.mutate(etapa.id, {
                        onSuccess: () => toast.success(`Etapa ${etapa.nome} reativada.`),
                        onError: falhar,
                      })
                    }
                  >
                    Reativar
                  </Button>
                )}
              </div>
            </li>
          ))}
        </ol>

        <ul aria-label="Etapas de fechamento" className="flex flex-col divide-y rounded-lg border">
          {fechamento.map((etapa) => (
            <li key={etapa.id} className="flex items-center gap-2 px-3 py-2">
              <span className="min-w-0 flex-1">
                <SeloEtapa etapa={etapa} />
              </span>
              <Button
                variant="ghost"
                size="icon"
                aria-label={`Renomear ${etapa.nome}`}
                onClick={() => setRenomeando(etapa)}
              >
                <PencilIcon />
              </Button>
            </li>
          ))}
        </ul>

        <NovoNome
          id="nova-etapa"
          rotulo="Nova etapa"
          textoDoBotao="Adicionar"
          tamanhoMaximo={TAMANHO_MAXIMO_ETAPA}
          aoCriar={async (nome) => {
            await criar.mutateAsync(nome)
            toast.success(`Etapa ${nome} criada.`)
          }}
        />
      </CardContent>

      {renomeando && (
        <RenomearDialog
          titulo={`Renomear ${renomeando.nome}`}
          nomeAtual={renomeando.nome}
          tamanhoMaximo={TAMANHO_MAXIMO_ETAPA}
          aoSalvar={(nome) => renomear.mutateAsync({ id: renomeando.id, nome })}
          aoFechar={() => setRenomeando(null)}
        />
      )}
      {desativando && (
        <DesativarEtapaDialog
          etapa={desativando}
          destinos={ativas.filter((etapa) => etapa.id !== desativando.id)}
          aoFechar={() => setDesativando(null)}
        />
      )}
    </Card>
  )
}

function MotivosDePerda() {
  const { data: motivos, isPending } = useMotivosPerda()
  const criar = useCriarMotivo()
  const editar = useEditarMotivo()
  const [renomeando, setRenomeando] = useState<MotivoPerdaResponse | null>(null)

  if (isPending || !motivos) {
    return <Carregando />
  }

  const ativos = motivos.filter((motivo) => motivo.ativo).length

  const alternar = (motivo: MotivoPerdaResponse, ativo: boolean) =>
    editar.mutate(
      { id: motivo.id, nome: motivo.nome, ativo },
      {
        onSuccess: () =>
          toast.success(
            ativo ? `${motivo.nome} voltou para a lista.` : `${motivo.nome} saiu da lista.`,
          ),
        onError: (erro) => toast.error(mensagemDoErro(erro)),
      },
    )

  return (
    <Card>
      <CardHeader>
        <CardTitle>Motivos de perda</CardTitle>
        <CardDescription>
          A atendente escolhe um deles ao levar um lead para Perdido. Um motivo desativado sai da
          lista, mas continua nos leads e no histórico.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ul aria-label="Motivos de perda" className="flex flex-col divide-y rounded-lg border">
          {motivos.map((motivo) => (
            <li key={motivo.id} className="flex items-center gap-2 px-3 py-2">
              <span className="min-w-0 flex-1 break-words">{motivo.nome}</span>
              <Button
                variant="ghost"
                size="icon"
                aria-label={`Renomear ${motivo.nome}`}
                onClick={() => setRenomeando(motivo)}
              >
                <PencilIcon />
              </Button>
              <Switch
                checked={motivo.ativo}
                aria-label={`Ativo: ${motivo.nome}`}
                // Sempre sobra um motivo ativo para escolher.
                disabled={motivo.ativo && ativos <= 1}
                onCheckedChange={(ativo) => alternar(motivo, ativo)}
              />
            </li>
          ))}
        </ul>
        <NovoNome
          id="novo-motivo"
          rotulo="Novo motivo"
          textoDoBotao="Adicionar"
          tamanhoMaximo={TAMANHO_MAXIMO_MOTIVO}
          aoCriar={async (nome) => {
            await criar.mutateAsync(nome)
            toast.success(`Motivo ${nome} criado.`)
          }}
        />
      </CardContent>
      {renomeando && (
        <RenomearDialog
          titulo={`Renomear ${renomeando.nome}`}
          nomeAtual={renomeando.nome}
          tamanhoMaximo={TAMANHO_MAXIMO_MOTIVO}
          aoSalvar={(nome) =>
            editar.mutateAsync({ id: renomeando.id, nome, ativo: renomeando.ativo })
          }
          aoFechar={() => setRenomeando(null)}
        />
      )}
    </Card>
  )
}

function Regras() {
  const { data: configuracoes } = useConfiguracoes()
  const editar = useEditarConfiguracoes()

  return (
    <Card>
      <CardHeader>
        <CardTitle>Regras</CardTitle>
      </CardHeader>
      <CardContent>
        <Field orientation="horizontal">
          <FieldContent>
            <FieldLabel htmlFor="exigir-documento">
              Exigir CPF/CNPJ válido para marcar Ganho
            </FieldLabel>
            <FieldDescription>
              Ligada, um lead só vai para Ganho com um CPF ou CNPJ válido na ficha.
            </FieldDescription>
          </FieldContent>
          <Switch
            id="exigir-documento"
            checked={configuracoes?.exigirDocumentoParaGanho ?? true}
            disabled={!configuracoes || editar.isPending}
            onCheckedChange={(exigir) =>
              editar.mutate(exigir, {
                onSuccess: () =>
                  toast.success(
                    exigir ? 'Ganho volta a exigir CPF/CNPJ.' : 'Ganho não exige mais CPF/CNPJ.',
                  ),
                onError: (erro) => toast.error(mensagemDoErro(erro)),
              })
            }
          />
        </Field>
      </CardContent>
    </Card>
  )
}
