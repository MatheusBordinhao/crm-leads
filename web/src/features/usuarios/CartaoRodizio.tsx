import { toast } from 'sonner'
import type { UsuarioResponse } from '@/api/cliente'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Switch } from '@/components/ui/switch'
import { useConfiguracoes, useEditarConfiguracoes } from '@/features/configuracoes/dados'
import { mensagemDoErro } from '@/lib/formularios'

/**
 * Distribuição automática (seção 3.3): a chave do rodízio e quem recebe o próximo lead. Quem participa do
 * rodízio se escolhe na coluna Rodízio da lista.
 */
export function CartaoRodizio({ usuarios }: { usuarios: UsuarioResponse[] }) {
  const { data: configuracoes } = useConfiguracoes()
  const editar = useEditarConfiguracoes()
  const ligada = configuracoes?.distribuicaoAutomatica ?? true
  const proximo = usuarios.find((usuario) => usuario.proximoDaVez)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Rodízio</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Field orientation="horizontal">
          <FieldContent>
            <FieldLabel htmlFor="distribuicao-automatica">
              Distribuir os leads novos pelo rodízio
            </FieldLabel>
            <FieldDescription>
              Os leads da integração e os que um admin cadastra sem escolher a responsável vão, um
              de cada vez, para quem está no rodízio. Desligado, eles ficam sem responsável e os
              admins são avisados.
            </FieldDescription>
          </FieldContent>
          <Switch
            id="distribuicao-automatica"
            checked={ligada}
            disabled={!configuracoes || editar.isPending}
            onCheckedChange={(distribuir) =>
              configuracoes &&
              editar.mutate(
                { ...configuracoes, distribuicaoAutomatica: distribuir },
                {
                  onSuccess: () =>
                    toast.success(
                      distribuir
                        ? 'Rodízio ligado.'
                        : 'Rodízio desligado: os leads novos ficam sem responsável.',
                    ),
                  onError: (erro) => toast.error(mensagemDoErro(erro)),
                },
              )
            }
          />
        </Field>
        {ligada && (
          <p className="text-sm">
            {proximo ? (
              <>
                O próximo lead vai para <span className="font-medium">{proximo.nome}</span>.
              </>
            ) : (
              'Ninguém está no rodízio: os leads novos ficam sem responsável. Ligue o rodízio de quem atende na lista.'
            )}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
