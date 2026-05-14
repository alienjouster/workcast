import type { Config } from 'tailwindcss';
import typography from '@tailwindcss/typography';

const config: Config = {
  content: [
    './components/**/*.{js,ts,jsx,tsx,mdx}',
    './app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      keyframes: {
        'pulse-glow': {
          '0%, 100%': { boxShadow: '0 0 0 0 rgba(239, 68, 68, 0.5)' },
          '50%': { boxShadow: '0 0 0 7px rgba(239, 68, 68, 0)' },
        },
      },
      animation: {
        'pulse-glow': 'pulse-glow 1.4s ease-in-out infinite',
      },
    },
  },
  plugins: [typography],
};
export default config;
