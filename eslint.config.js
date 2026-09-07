import eslint from '@eslint/js';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  {
    ignores: ['dist/**', 'node_modules/**', 'public/**', 'assets/**'],
  },
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
);
