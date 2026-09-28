import type { ReactNode } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Marca } from './Marca'

/** Tela com um cartão no centro, fora do layout com menu: login e troca de senha. */
export function TelaCentral({
  titulo,
  descricao,
  children,
}: {
  titulo: string
  descricao?: string
  children: ReactNode
}) {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 bg-muted p-4">
      <Marca className="text-lg" />
      <Card className="w-full max-w-sm">
        <CardHeader>
          <CardTitle>
            <h1>{titulo}</h1>
          </CardTitle>
          {descricao && <CardDescription>{descricao}</CardDescription>}
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
    </main>
  )
}
