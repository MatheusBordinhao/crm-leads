import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeftIcon } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { toast } from 'sonner'
import { z } from 'zod'
import { CampoSelecao } from '@/components/CampoSelecao'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { useEu } from '@/features/acesso/sessao'
import { useUsuarios } from '@/features/usuarios/dados'
import { aplicarErrosDaApi } from '@/lib/formularios'
import { CamposDadosLead } from './CamposDadosLead'
import { useCriarLead } from './dados'
import { CAMPOS_DADOS, camposDados, dadosIniciais, dadosParaApi, validarDados } from './esquema'
import { ORIGENS_DO_CADASTRO, ROTULOS_ORIGEM } from './rotulos'

const esquema = z
  .object({
    ...camposDados,
    origem: z.enum(ORIGENS_DO_CADASTRO),
    responsavelId: z.string(),
  })
  .superRefine(validarDados())

/** Pessoa que já é lead de outra atendente: os dados vão para a ficha dela, sem dizer qual é nem de quem. */
const MENSAGEM_LEAD_DE_OUTRA_PESSOA = 'Já existe um lead com esses dados.'

/**
 * Cadastro à mão. Passa pelo mesmo caminho das outras entradas: se a pessoa já é um lead, os dados vão para
 * a ficha que existe.
 */
export function NovoLeadPage() {
  const { data: eu } = useEu()
  const ehAdmin = eu?.perfil === 'Admin'
  const { data: usuarios } = useUsuarios({ enabled: ehAdmin })
  const criar = useCriarLead()
  const navigate = useNavigate()
  const [erroGeral, setErroGeral] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(esquema),
    defaultValues: { ...dadosIniciais(), origem: 'manual' as const, responsavelId: '' },
  })

  const salvar = handleSubmit(({ origem, responsavelId, ...dados }) => {
    setErroGeral(null)
    criar.mutate(
      {
        ...dadosParaApi(dados),
        origem,
        // Só o admin escolhe (sem escolha, o rodízio decide); a atendente fica com o lead que cadastra.
        responsavelId: ehAdmin && responsavelId ? responsavelId : null,
      },
      {
        onSuccess: ({ leadId, situacao, podeAbrir }) => {
          if (!podeAbrir) {
            toast.info(MENSAGEM_LEAD_DE_OUTRA_PESSOA)
            navigate('/leads')
            return
          }
          toast.success(
            situacao === 'criado'
              ? 'Lead cadastrado.'
              : 'Essa pessoa já era um lead. Os dados novos foram acrescentados à ficha.',
          )
          navigate(`/leads/${leadId}`)
        },
        onError: (erro) =>
          setErroGeral(
            aplicarErrosDaApi(erro, setError, [...CAMPOS_DADOS, 'origem', 'responsavelId']),
          ),
      },
    )
  })

  return (
    <section className="flex max-w-3xl flex-col gap-4">
      <Link
        to="/leads"
        className="flex items-center gap-1 self-start text-sm text-muted-foreground hover:underline"
      >
        <ArrowLeftIcon className="size-4" aria-hidden />
        Leads
      </Link>
      <h1 className="text-xl font-semibold">Novo lead</h1>

      <Card>
        <CardContent>
          <form onSubmit={salvar} noValidate className="flex flex-col gap-5">
            {erroGeral && (
              <Alert variant="destructive">
                <AlertDescription>{erroGeral}</AlertDescription>
              </Alert>
            )}
            <CamposDadosLead register={register} errors={errors} />
            <div className="grid gap-5 md:grid-cols-2">
              <CampoSelecao
                id="origem"
                rotulo="Origem"
                registro={register('origem')}
                erro={errors.origem}
              >
                {ORIGENS_DO_CADASTRO.map((origem) => (
                  <option key={origem} value={origem}>
                    {ROTULOS_ORIGEM[origem]}
                  </option>
                ))}
              </CampoSelecao>
              {ehAdmin && (
                <CampoSelecao
                  id="responsavelId"
                  rotulo="Responsável"
                  descricao="No rodízio, o lead vai para a próxima pessoa da vez. Se ninguém estiver no rodízio, ou a distribuição estiver desligada, ele fica sem responsável e só os admins o veem."
                  registro={register('responsavelId')}
                  erro={errors.responsavelId}
                >
                  <option value="">Rodízio</option>
                  {usuarios
                    ?.filter((usuario) => usuario.ativo)
                    .map((usuario) => (
                      <option key={usuario.id} value={usuario.id}>
                        {usuario.nome}
                      </option>
                    ))}
                </CampoSelecao>
              )}
            </div>
            <div className="flex flex-wrap justify-end gap-2">
              <Button asChild variant="outline">
                <Link to="/leads">Cancelar</Link>
              </Button>
              <Button type="submit" disabled={criar.isPending}>
                {criar.isPending ? 'Salvando…' : 'Cadastrar lead'}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </section>
  )
}
