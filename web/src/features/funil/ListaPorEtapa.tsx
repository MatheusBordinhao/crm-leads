import { useState } from 'react'
import { Link } from 'react-router'
import type { FiltrosFunil, QuadroFunilResponse } from '@/api/cliente'
import { Selecao } from '@/components/Selecao'
import { Label } from '@/components/ui/label'
import { CartaoLead } from './CartaoLead'
import { useMovimento } from './useMovimento'

type Props = {
  quadro: QuadroFunilResponse
  filtros: FiltrosFunil
  mostrarResponsavel: boolean
}

/**
 * O funil no celular (seção 7): uma etapa por vez, escolhida no seletor, e cada cartão com um "Mover para",
 * sem arrastar.
 */
export function ListaPorEtapa({ quadro, filtros, mostrarResponsavel }: Props) {
  const { soltar, pendenteId, dialogo } = useMovimento(filtros)
  const [etapaId, setEtapaId] = useState<number | null>(null)
  const coluna = quadro.colunas.find((c) => c.etapa.id === etapaId) ?? quadro.colunas[0]
  const destinos = [...quadro.colunas.map((c) => c.etapa), quadro.ganho.etapa, quadro.perdido.etapa]

  if (!coluna) {
    return null
  }

  return (
    <section aria-label="Leads por etapa" className="flex flex-col gap-3 md:hidden">
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="etapa-da-lista">Etapa</Label>
        <Selecao
          id="etapa-da-lista"
          value={coluna.etapa.id}
          onChange={(evento) => setEtapaId(Number(evento.target.value))}
        >
          {quadro.colunas.map((c) => (
            <option key={c.etapa.id} value={c.etapa.id}>
              {c.etapa.nome} ({c.total})
            </option>
          ))}
        </Selecao>
      </div>
      <p className="text-sm text-muted-foreground">
        Nos últimos 30 dias: {quadro.ganho.periodo} em {quadro.ganho.etapa.nome} e{' '}
        {quadro.perdido.periodo} em {quadro.perdido.etapa.nome}.
      </p>

      <ul aria-label={`Leads em ${coluna.etapa.nome}`} className="flex flex-col gap-3">
        {coluna.leads
          .filter((cartao) => cartao.id !== pendenteId)
          .map((cartao) => (
            <li key={cartao.id}>
              <CartaoLead
                cartao={cartao}
                mostrarResponsavel={mostrarResponsavel}
                rodape={
                  // Controlado e sempre vazio: depois de escolher, volta ao "Mover para…".
                  <Selecao
                    aria-label={`Mover ${cartao.nome} para`}
                    value=""
                    onChange={(evento) => {
                      const destino = destinos.find((d) => d.id === Number(evento.target.value))
                      if (destino) {
                        soltar(cartao, coluna.etapa.id, destino)
                      }
                    }}
                  >
                    <option value="">Mover para…</option>
                    {destinos
                      .filter((destino) => destino.id !== coluna.etapa.id)
                      .map((destino) => (
                        <option key={destino.id} value={destino.id}>
                          {destino.nome}
                        </option>
                      ))}
                  </Selecao>
                }
              />
            </li>
          ))}
      </ul>
      {coluna.leads.length === 0 && (
        <p className="py-4 text-center text-sm text-muted-foreground">Nenhum lead nesta etapa.</p>
      )}
      {coluna.total > coluna.leads.length && (
        <Link to={`/leads?etapa=${coluna.etapa.id}`} className="text-sm hover:underline">
          Ver todos os {coluna.total} na lista
        </Link>
      )}
      {dialogo}
    </section>
  )
}
