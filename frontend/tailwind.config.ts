import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        primary: '#ec5b13',
        'background-light': '#f8f6f6',
        'background-dark': '#221610',
      },
      fontFamily: {
        display: ['Public Sans', 'sans-serif'],
      },
    },
  },
  plugins: [],
} satisfies Config
