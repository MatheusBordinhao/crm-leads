import { EllipsisIcon, KeyRoundIcon, PencilIcon, PlusIcon } from 'lucide-react'
import { useState } from 'react'
import { toast } from 'sonner'
import type { UsuarioResponse } from '@/api/cliente'
import { Carregando } from '@/components/Carregando'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Switch } from '@/components/ui/switch'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { useEu } from '@/features/acesso/sessao'
import { formatarHora } from '@/lib/datas'
import { useEditarUsuario, useUsuarios } from './dados'
import { RedefinirSenhaDialog } from './RedefinirSenhaDialog'
import { UsuarioDialog } from './UsuarioDialog'

type Dialogo =
  | { tipo: 'novo' }
  | { tipo: 'editar'; usuario: UsuarioResponse }
  | { tipo: 'senha'; usuario: UsuarioResponse }

type Alternavel = 'ativo' | 'participaRodizio'

export function UsuariosPage() {
  const { data: eu } = useEu()
  const { data: usuarios, isPending, isError, refetch } = useUsuarios()
  const editar = useEditarUsuario()
  const [dialogo, setDialogo] = useState<Dialogo | null>(null)

  /** Interruptores da lista: salvam na hora, mandando os outros dados como estão. */
  const alternar = (usuario: UsuarioResponse, campo: Alternavel, valor: boolean) => {
    const { nome, email, perfil, ativo, participaRodizio } = usuario
    editar.mutate(
      {
        id: usuario.id,
        dados: { nome, email, perfil, ativo, participaRodizio, [campo]: valor },
      },
      {
        onSuccess: () => toast.success(mensagemDaAlteracao(usuario.nome, campo, valor)),
        onError: (erro) => toast.error(erro.message),
      },
    )
  }

  const props = (usuario: UsuarioResponse): PropsLinha => ({
    usuario,
    ehOProprio: usuario.id === eu?.id,
    aoAlternar: (campo, valor) => alternar(usuario, campo, valor),
    aoEditar: () => setDialogo({ tipo: 'editar', usuario }),
    aoRedefinirSenha: () => setDialogo({ tipo: 'senha', usuario }),
  })

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold">Usuários</h1>
        <Button onClick={() => setDialogo({ tipo: 'novo' })}>
          <PlusIcon aria-hidden />
          Novo usuário
        </Button>
      </div>

      {isPending && <Carregando />}
      {isError && (
        <div className="flex flex-col items-start gap-2">
          <p>Não foi possível carregar os usuários.</p>
          <Button variant="outline" onClick={() => refetch()}>
            Tentar de novo
          </Button>
        </div>
      )}

      {usuarios && (
        <>
          {/* Computador: tabela */}
          <div className="hidden rounded-lg border md:block">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nome</TableHead>
                  <TableHead>Perfil</TableHead>
                  <TableHead>Situação</TableHead>
                  <TableHead>Rodízio</TableHead>
                  <TableHead>Ativo</TableHead>
                  <TableHead>
                    <span className="sr-only">Ações</span>
                  </TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {usuarios.map((usuario) => (
                  <LinhaTabela key={usuario.id} {...props(usuario)} />
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Celular: cartões */}
          <ul aria-label="Usuários" className="flex flex-col gap-3 md:hidden">
            {usuarios.map((usuario) => (
              <li key={usuario.id}>
                <CartaoUsuario {...props(usuario)} />
              </li>
            ))}
          </ul>
        </>
      )}

      {dialogo?.tipo === 'novo' && (
        <UsuarioDialog ehOProprio={false} aoFechar={() => setDialogo(null)} />
      )}
      {dialogo?.tipo === 'editar' && (
        <UsuarioDialog
          usuario={dialogo.usuario}
          ehOProprio={dialogo.usuario.id === eu?.id}
          aoFechar={() => setDialogo(null)}
        />
      )}
      {dialogo?.tipo === 'senha' && (
        <RedefinirSenhaDialog usuario={dialogo.usuario} aoFechar={() => setDialogo(null)} />
      )}
    </section>
  )
}

function mensagemDaAlteracao(nome: string, campo: Alternavel, valor: boolean) {
  if (campo === 'ativo') {
    return valor ? `${nome} foi reativado.` : `${nome} foi desativado.`
  }
  return valor ? `${nome} entrou no rodízio.` : `${nome} saiu do rodízio.`
}

type PropsLinha = {
  usuario: UsuarioResponse
  ehOProprio: boolean
  aoAlternar: (campo: Alternavel, valor: boolean) => void
  aoEditar: () => void
  aoRedefinirSenha: () => void
}

function LinhaTabela({ usuario, ehOProprio, aoAlternar, aoEditar, aoRedefinirSenha }: PropsLinha) {
  return (
    <TableRow>
      <TableCell>
        <div className="font-medium">{usuario.nome}</div>
        <div className="text-muted-foreground">{usuario.email}</div>
      </TableCell>
      <TableCell>
        <BadgePerfil usuario={usuario} />
      </TableCell>
      <TableCell>
        <Situacao usuario={usuario} />
      </TableCell>
      <TableCell>
        <Switch
          checked={usuario.participaRodizio}
          onCheckedChange={(valor) => aoAlternar('participaRodizio', valor)}
          aria-label={`Rodízio: ${usuario.nome}`}
        />
      </TableCell>
      <TableCell>
        <Switch
          checked={usuario.ativo}
          onCheckedChange={(valor) => aoAlternar('ativo', valor)}
          disabled={ehOProprio}
          aria-label={`Ativo: ${usuario.nome}`}
        />
      </TableCell>
      <TableCell className="text-right">
        <Acoes usuario={usuario} aoEditar={aoEditar} aoRedefinirSenha={aoRedefinirSenha} />
      </TableCell>
    </TableRow>
  )
}

function CartaoUsuario({
  usuario,
  ehOProprio,
  aoAlternar,
  aoEditar,
  aoRedefinirSenha,
}: PropsLinha) {
  const id = `usuario-${usuario.id}`
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <div className="truncate font-medium">{usuario.nome}</div>
            <div className="truncate text-sm text-muted-foreground">{usuario.email}</div>
          </div>
          <Acoes usuario={usuario} aoEditar={aoEditar} aoRedefinirSenha={aoRedefinirSenha} />
        </div>
        <div className="flex flex-wrap gap-1">
          <BadgePerfil usuario={usuario} />
          <Situacao usuario={usuario} />
        </div>
        <div className="flex flex-wrap gap-6">
          <div className="flex items-center gap-2">
            <Switch
              id={`${id}-rodizio`}
              checked={usuario.participaRodizio}
              onCheckedChange={(valor) => aoAlternar('participaRodizio', valor)}
            />
            <Label htmlFor={`${id}-rodizio`}>Rodízio</Label>
          </div>
          <div className="flex items-center gap-2">
            <Switch
              id={`${id}-ativo`}
              checked={usuario.ativo}
              onCheckedChange={(valor) => aoAlternar('ativo', valor)}
              disabled={ehOProprio}
            />
            <Label htmlFor={`${id}-ativo`}>Ativo</Label>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

function BadgePerfil({ usuario }: { usuario: UsuarioResponse }) {
  return (
    <Badge variant={usuario.perfil === 'Admin' ? 'default' : 'secondary'}>{usuario.perfil}</Badge>
  )
}

function Situacao({ usuario }: { usuario: UsuarioResponse }) {
  return (
    <div className="flex flex-wrap gap-1">
      {!usuario.ativo && <Badge variant="outline">Desativado</Badge>}
      {usuario.bloqueadoAte && (
        <Badge variant="destructive">Bloqueado até {formatarHora(usuario.bloqueadoAte)}</Badge>
      )}
      {usuario.trocarSenha && <Badge variant="outline">Senha provisória</Badge>}
    </div>
  )
}

function Acoes({
  usuario,
  aoEditar,
  aoRedefinirSenha,
}: Pick<PropsLinha, 'usuario' | 'aoEditar' | 'aoRedefinirSenha'>) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="ghost" size="icon" aria-label={`Ações de ${usuario.nome}`}>
          <EllipsisIcon />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={aoEditar}>
          <PencilIcon aria-hidden />
          Editar
        </DropdownMenuItem>
        <DropdownMenuItem onSelect={aoRedefinirSenha}>
          <KeyRoundIcon aria-hidden />
          Redefinir senha
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
