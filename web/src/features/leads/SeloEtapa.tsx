import type { LeadResumoResponse } from '@/api/cliente'
import { Badge } from '@/components/ui/badge'
import { varianteEtapa } from './rotulos'

export function SeloEtapa({ etapa }: { etapa: LeadResumoResponse['etapa'] }) {
  return <Badge variant={varianteEtapa(etapa.tipo)}>{etapa.nome}</Badge>
}
