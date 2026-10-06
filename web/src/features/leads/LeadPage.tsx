import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeftIcon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useParams } from 'react-router'
import { toast } from 'sonner'
import { ErroApi, type LeadResponse, type TipoInteracao } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { useEu } from '@/features/acesso/sessao'
import { formatarDataHora } from '@/lib/datas'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { z } from '@/lib/zod'
import { BotoesContato } from './BotoesContato'
import { CamposDadosLead } from './CamposDadosLead'
import { useEditarLead, useLead } from './dados'
import { EtapaDoLead } from './EtapaDoLead'
import { CAMPOS_DADOS, camposDados, dadosIniciais, dadosParaApi, validarDados } from './esquema'
import { InteracaoDialog } from './InteracaoDialog'
import { LinhaDoTempo } from './LinhaDoTempo'
import { ResponsavelDoLead } from './ResponsavelDoLead'
import { ROTULOS_ORIGEM, rotuloPlataforma } from './rotulos'
import { SeloEtapa } from './SeloEtapa'

export function LeadPage() {
  const id = Number(useParams().id)
  const { data: lead, error, isError, refetch } = useLead(id)

  const naoEncontrado =
    !Number.isSafeInteger(id) || id <= 0 || (error instanceof ErroApi && error.status === 404)
  if (naoEncontrado) {
    return (
      <section className="flex flex-col items-start gap-3">
        <h1 className="text-xl font-semibold">Lead não encontrado</h1>
        <p className="text-muted-foreground">O lead não existe ou está com outra pessoa.</p>
        <Button asChild variant="outline">
          <Link to="/leads">Voltar para os leads</Link>
        </Button>
      </section>
    )
  }
  if (isError) {
    return (
      <div className="flex flex-col items-start gap-2">
        <p>Não foi possível carregar o lead.</p>
        <Button variant="outline" onClick={() => refetch()}>
          Tentar de novo
        </Button>
      </div>
    )
  }
  if (!lead) {
    return <Carregando />
  }

  return <FichaLead lead={lead} aoRecarregar={() => refetch()} />
}

type Editar = ReturnType<typeof useEditarLead>

/**
 * Chave do formulário de dados: muda quando os dados pessoais mudam (salvos agora ou recarregados), e não
 * quando só a etapa ou o primeiro contato mudam. Assim, mudar a etapa não apaga o que a pessoa digitou.
 */
function chaveDosDados(lead: LeadResponse) {
  return [lead.nome, lead.email, lead.telefone, lead.cpfCnpj, lead.uf, lead.empresa].join('\u001f')
}

function FichaLead({ lead, aoRecarregar }: { lead: LeadResponse; aoRecarregar: () => void }) {
  const { data: eu } = useEu()
  // Fica aqui, e não no formulário, porque o formulário é recriado quando os dados mudam: os callbacks da
  // edição (o aviso de "salvo") se perderiam se o componente que a disparou sumisse.
  const editar = useEditarLead(lead.id)
  const [registrando, setRegistrando] = useState<TipoInteracao | null>(null)
  // A ficha como estava quando a pessoa começou a editar os dados. Enquanto ela edita, o formulário fica com
  // essa base: uma mudança que chegue pelo tempo real não apaga o que ela digitou.
  const [base, setBase] = useState<LeadResponse | null>(null)
  const mudouPorFora = base !== null && chaveDosDados(base) !== chaveDosDados(lead)

  return (
    <section className="flex flex-col gap-4">
      <Link
        to="/leads"
        className="flex items-center gap-1 self-start text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeftIcon className="size-4" aria-hidden />
        Leads
      </Link>

      <header className="flex flex-col gap-2">
        <h1 className="text-xl font-semibold break-words">{lead.nome}</h1>
        <div className="flex flex-wrap items-center gap-1">
          <SeloEtapa etapa={lead.etapa} />
          {lead.teste && <Badge variant="outline">Teste</Badge>}
          {lead.anonimizadoEm && <Badge variant="outline">Anonimizado</Badge>}
        </div>
        <p className="text-sm text-muted-foreground">
          Entrou em {formatarDataHora(lead.criadoEm)} · {ROTULOS_ORIGEM[lead.origem]}
          {lead.campanha && ` · ${lead.campanha.nome}`}
        </p>
        <p className="text-sm text-muted-foreground">
          {lead.primeiroContatoEm
            ? `Primeiro contato em ${formatarDataHora(lead.primeiroContatoEm)}`
            : 'Ainda sem contato'}
        </p>
      </header>

      <BotoesContato lead={lead} aoContatar={setRegistrando} />

      {/* No celular, uma coluna: etapa (e responsável, para o admin), dados, linha do tempo e origem. No
          computador, dados e linha do tempo à esquerda; etapa, responsável e origem à direita. */}
      <div className="grid items-start gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="flex flex-col gap-4 lg:col-start-2 lg:row-start-1">
          <EtapaDoLead lead={lead} />
          {eu?.perfil === 'Admin' && <ResponsavelDoLead lead={lead} />}
        </div>
        <Card className="lg:col-start-1 lg:row-span-2 lg:row-start-1">
          <CardHeader>
            <CardTitle>Dados</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            {mudouPorFora && (
              <Alert>
                <AlertDescription className="flex flex-col items-start gap-2">
                  Outra pessoa alterou os dados deste lead.
                  <Button type="button" variant="outline" size="sm" onClick={() => setBase(null)}>
                    Ver a versão atual
                  </Button>
                </AlertDescription>
              </Alert>
            )}
            <FormularioDados
              key={chaveDosDados(base ?? lead)}
              lead={base ?? lead}
              // A versão mais recente, para uma mudança de etapa feita aqui mesmo não contar como conflito. Se
              // outra pessoa mudou os dados, vai a da base, e a API responde o conflito em vez de apagar a mudança.
              versao={mudouPorFora ? base.versao : lead.versao}
              editar={editar}
              aoEditar={() => setBase((atual) => atual ?? lead)}
              aoTerminar={() => setBase(null)}
              aoRecarregar={aoRecarregar}
            />
          </CardContent>
        </Card>
        <Card className="lg:col-start-1 lg:row-start-3">
          <CardHeader>
            <CardTitle>Linha do tempo</CardTitle>
          </CardHeader>
          <CardContent>
            <LinhaDoTempo leadId={lead.id} />
          </CardContent>
        </Card>
        <Card className="lg:col-start-2 lg:row-start-2">
          <CardHeader>
            <CardTitle>Origem</CardTitle>
          </CardHeader>
          <CardContent>
            <DadosDaOrigem lead={lead} />
          </CardContent>
        </Card>
      </div>

      {registrando && (
        <InteracaoDialog
          leadId={lead.id}
          tipoInicial={registrando}
          aoFechar={() => setRegistrando(null)}
        />
      )}
    </section>
  )
}

function FormularioDados({
  lead,
  versao,
  editar,
  aoEditar,
  aoTerminar,
  aoRecarregar,
}: {
  lead: LeadResponse
  /** A versão que vai no salvar, para a API responder o conflito se o lead mudou depois dela. */
  versao: number
  editar: Editar
  /** A pessoa mexeu num campo: a ficha prende a base do formulário. */
  aoEditar: () => void
  /** A edição acabou (salva, descartada ou recarregada): o formulário volta a mostrar a versão atual. */
  aoTerminar: () => void
  aoRecarregar: () => void
}) {
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const [conflito, setConflito] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(
      z
        .object(camposDados)
        .superRefine(
          validarDados({ email: lead.email, telefone: lead.telefone, cpfCnpj: lead.cpfCnpj }),
        ),
    ),
    defaultValues: dadosIniciais(lead),
  })

  const salvar = handleSubmit((dados) => {
    setErroGeral(null)
    editar.mutate(
      { ...dadosParaApi(dados), versao },
      {
        onSuccess: () => {
          toast.success('Dados salvos.')
          aoTerminar()
        },
        onError: (erro) => {
          if (erro instanceof ErroApi && erro.status === 409) {
            setConflito(erro.message)
          } else {
            setErroGeral(aplicarErrosDaApi(erro, setError, CAMPOS_DADOS))
          }
        },
      },
    )
  })

  return (
    <form onSubmit={salvar} onChange={aoEditar} noValidate className="flex flex-col gap-5">
      {conflito && (
        <Alert variant="destructive">
          <AlertDescription className="flex flex-col items-start gap-2">
            {conflito}
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                // Se só a etapa mudou, os dados continuam iguais e o formulário não é recriado.
                setConflito(null)
                aoTerminar()
                aoRecarregar()
              }}
            >
              Recarregar
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {erroGeral && (
        <Alert variant="destructive">
          <AlertDescription>{erroGeral}</AlertDescription>
        </Alert>
      )}
      <CamposDadosLead register={register} errors={errors} avisos={lead.avisos} />
      <div className="flex flex-wrap justify-end gap-2">
        <Button
          type="button"
          variant="outline"
          disabled={!isDirty}
          onClick={() => {
            reset()
            aoTerminar()
          }}
        >
          Descartar
        </Button>
        <Button type="submit" disabled={!isDirty || editar.isPending}>
          {editar.isPending ? 'Salvando…' : 'Salvar'}
        </Button>
      </div>
    </form>
  )
}

function DadosDaOrigem({ lead }: { lead: LeadResponse }) {
  const utms: [string, string | null][] = [
    ['UTM source', lead.utmSource],
    ['UTM medium', lead.utmMedium],
    ['UTM campaign', lead.utmCampaign],
    ['UTM content', lead.utmContent],
    ['UTM term', lead.utmTerm],
    ['GCLID', lead.gclid],
  ]

  return (
    <div className="flex flex-col gap-4">
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        <Item rotulo="Origem">{ROTULOS_ORIGEM[lead.origem]}</Item>
        {lead.plataforma && <Item rotulo="Plataforma">{rotuloPlataforma(lead.plataforma)}</Item>}
        {lead.campanha && <Item rotulo="Campanha">{lead.campanha.nome}</Item>}
        {utms.map(
          ([rotulo, valor]) =>
            valor && (
              <Item key={rotulo} rotulo={rotulo}>
                {valor}
              </Item>
            ),
        )}
        <Item rotulo="Atualizado em">{formatarDataHora(lead.atualizadoEm)}</Item>
      </dl>
      {lead.respostasExtras.length > 0 && (
        <div className="flex flex-col gap-2">
          <h3 className="text-sm font-medium">Respostas do formulário</h3>
          <dl className="flex flex-col gap-2 text-sm">
            {lead.respostasExtras.map((resposta) => (
              <div key={resposta.pergunta}>
                <dt className="text-muted-foreground">{resposta.pergunta}</dt>
                <dd className="break-words">{resposta.resposta}</dd>
              </div>
            ))}
          </dl>
        </div>
      )}
    </div>
  )
}

function Item({ rotulo, children }: { rotulo: string; children: ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{rotulo}</dt>
      <dd className="min-w-0 break-words">{children}</dd>
    </>
  )
}
