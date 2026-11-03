import type { Config } from 'tailwindcss'

export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        // #c2410c: 5,2:1 contra branco (AA). O laranja anterior (#ec5b13) dava 3,4:1.
        primary: { DEFAULT: '#c2410c', hover: '#9a3412' },
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
