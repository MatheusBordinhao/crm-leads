import { z } from 'zod'

// A CSP de produção (deploy/Caddyfile) não tem 'unsafe-eval'. Sem o jitless, o Zod testa o new Function ao criar
// cada esquema de objeto, e o navegador registra a violação da CSP, mesmo com o erro tratado. Os esquemas são criados
// quando os módulos carregam: por isso todo módulo importa o z daqui (o ESLint barra o import de 'zod'), e a
// configuração vale antes de qualquer esquema.
z.config({ jitless: true })

export { z }
