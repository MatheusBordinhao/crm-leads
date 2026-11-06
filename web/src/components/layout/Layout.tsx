import { MenuIcon } from 'lucide-react'
import { useState } from 'react'
import { Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { useEu } from '@/features/acesso/sessao'
import { Sino } from '@/features/notificacoes/Sino'
import { TempoReal } from '@/features/tempo-real/TempoReal'
import { Marca } from './Marca'
import { Menu } from './Menu'
import { MenuUsuario } from './MenuUsuario'

/**
 * Layout das telas com sessão: menu lateral no computador; no celular, barra no topo com o menu numa
 * gaveta. Funciona a partir de 360 px. Com ele na tela, a conexão do tempo real fica aberta.
 */
export function Layout() {
  const { data: eu } = useEu()
  const [gavetaAberta, setGavetaAberta] = useState(false)

  // A RotaProtegida só mostra o layout com sessão.
  if (!eu) {
    return null
  }

  return (
    <div className="min-h-svh md:grid md:grid-cols-[15rem_1fr]">
      <aside className="hidden border-r bg-sidebar md:block">
        <div className="flex h-14 items-center border-b px-4">
          <Marca />
        </div>
        <Menu perfil={eu.perfil} />
      </aside>

      <div className="flex min-h-svh min-w-0 flex-col">
        <header className="flex h-14 items-center gap-2 border-b px-2 md:px-4">
          <Sheet open={gavetaAberta} onOpenChange={setGavetaAberta}>
            <SheetTrigger asChild>
              <Button variant="ghost" size="icon" className="md:hidden" aria-label="Abrir o menu">
                <MenuIcon />
              </Button>
            </SheetTrigger>
            <SheetContent side="left" className="w-64 p-0">
              <SheetHeader className="border-b">
                <SheetTitle>
                  <Marca />
                </SheetTitle>
              </SheetHeader>
              <Menu perfil={eu.perfil} aoNavegar={() => setGavetaAberta(false)} />
            </SheetContent>
          </Sheet>
          <Marca className="md:hidden" />
          <div className="ml-auto flex items-center gap-1">
            <Sino />
            <MenuUsuario eu={eu} />
          </div>
        </header>
        <main className="flex-1 p-4 md:p-6">
          <Outlet />
          {/* O espaço dos avisos, que saem embaixo em todas as larguras: o maior, o "Avisos do
              navegador" em 360 px, ocupa 127 px com a margem. Sem ele, com a página rolada até o fim, o
              aviso cobria o último botão, que a rolagem não tira de baixo dele. */}
          <div aria-hidden className="h-32" />
        </main>
      </div>
      <TempoReal />
    </div>
  )
}
