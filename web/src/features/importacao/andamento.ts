/**
 * De quanto em quanto tempo a tela consulta o andamento de uma importação que ainda processa. Fica num módulo próprio
 * para os testes trocarem o valor (`vi.mock`) sem esperar 1 s de verdade a cada consulta.
 */
export const INTERVALO_ANDAMENTO = 1000
