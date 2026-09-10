import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import hooks from 'eslint-plugin-react-hooks'
import boundaries from 'eslint-plugin-boundaries'
import globals from 'globals'

export default tseslint.config(
  {
    ignores: [
      'node_modules/**',
      'dist/**',
      '.tanstack/**',
      'src/routeTree.gen.ts',
      'test-results/**',
      'playwright-report/**',
    ],
  },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  { languageOptions: { globals: { ...globals.browser, ...globals.node } } },
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { 'react-hooks': hooks, boundaries },
    settings: {
      'boundaries/root-path': import.meta.dirname,
      'boundaries/elements': [
        { type: 'feature', pattern: 'src/features/*', capture: ['feature'] },
        ...['primitives', 'common', 'layout'].map((type) => ({
          type,
          pattern: `src/components/${type}`,
        })),
        ...['lib', 'routes', 'styles'].map((type) => ({
          type,
          pattern: `src/${type}`,
        })),
      ],
      'import/resolver': { typescript: { project: './tsconfig.json' } },
    },
    rules: {
      ...hooks.configs.recommended.rules,
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_' },
      ],
      'boundaries/dependencies': [
        'error',
        {
          default: 'allow',
          policies: [
            {
              from: { element: { type: ['lib', 'primitives'] } },
              disallow: {
                to: {
                  element: { type: ['feature', 'routes', 'common', 'layout'] },
                },
              },
            },
            {
              from: { element: { type: 'common' } },
              disallow: {
                to: { element: { type: ['feature', 'routes', 'layout'] } },
              },
            },
            {
              from: {
                element: {
                  type: ['feature', 'lib', 'primitives', 'common', 'layout'],
                },
              },
              disallow: { to: { element: { type: 'routes' } } },
            },
            {
              to: { element: { type: 'feature' } },
              disallow: { to: { element: { fileInternalPath: '!index.ts' } } },
              message:
                'Importa una feature solo tramite la public API index.ts.',
            },
          ],
        },
      ],
    },
  },
)
