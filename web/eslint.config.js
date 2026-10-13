import js from '@eslint/js'
import prettier from 'eslint-config-prettier/flat'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import { defineConfig, globalIgnores } from 'eslint/config'

const MENSAGEM_ZOD =
  "Importe o z de '@/lib/zod', que desliga o new Function que a CSP de produção bloqueia."

export default defineConfig([
  // schema.d.ts é gerado pelo npm run api:tipos.
  globalIgnores(['dist', 'coverage', 'src/api/schema.d.ts']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      globals: globals.browser,
    },
  },
  // Os componentes do shadcn/ui exportam as variantes (buttonVariants etc.) junto com o componente.
  {
    files: ['src/components/ui/**/*.tsx'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  // O z vem de src/lib/zod.ts, que liga o jitless antes de qualquer esquema: a CSP de produção bloqueia o
  // new Function que o Zod testaria ao criar cada esquema de objeto.
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/lib/zod.ts'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [{ name: 'zod', message: MENSAGEM_ZOD }],
          patterns: [{ group: ['zod/*'], message: MENSAGEM_ZOD }],
        },
      ],
    },
  },
  // O service worker é JavaScript puro, servido como está (public/sw.js), num escopo de service worker.
  {
    files: ['public/sw.js'],
    extends: [js.configs.recommended],
    languageOptions: {
      sourceType: 'script',
      globals: globals.serviceworker,
    },
  },
  // Por último: desliga as regras de estilo que o Prettier cuida.
  prettier,
])
