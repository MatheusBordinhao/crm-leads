import { useCallback, useSyncExternalStore } from 'react'

/** Se a consulta de mídia (`matchMedia`) vale agora; atualiza quando a largura da janela muda. */
export function useConsultaDeMidia(consulta: string): boolean {
  const assinar = useCallback(
    (avisar: () => void) => {
      const lista = window.matchMedia(consulta)
      lista.addEventListener('change', avisar)
      return () => lista.removeEventListener('change', avisar)
    },
    [consulta],
  )
  return useSyncExternalStore(assinar, () => window.matchMedia(consulta).matches)
}
