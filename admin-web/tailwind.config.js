/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        'semi-color-primary': 'var(--semi-color-primary)',
        'semi-color-primary-hover': 'var(--semi-color-primary-hover)',
        'semi-color-primary-light-default': 'var(--semi-color-primary-light-default)',
        'semi-color-primary-light-hover': 'var(--semi-color-primary-light-hover)',
        'semi-color-success': 'var(--semi-color-success)',
        'semi-color-success-light-default': 'var(--semi-color-success-light-default)',
        'semi-color-warning': 'var(--semi-color-warning)',
        'semi-color-warning-light-default': 'var(--semi-color-warning-light-default)',
        'semi-color-danger': 'var(--semi-color-danger)',
        'semi-color-danger-light-default': 'var(--semi-color-danger-light-default)',
        'semi-color-info': 'var(--semi-color-info)',
        'semi-color-border': 'var(--semi-color-border)',
        'semi-color-fill-0': 'var(--semi-color-fill-0)',
        'semi-color-fill-1': 'var(--semi-color-fill-1)',
        'semi-color-fill-2': 'var(--semi-color-fill-2)',
        'semi-color-bg-0': 'var(--semi-color-bg-0)',
        'semi-color-bg-1': 'var(--semi-color-bg-1)',
        'semi-color-bg-2': 'var(--semi-color-bg-2)',
        'semi-color-text-0': 'var(--semi-color-text-0)',
        'semi-color-text-1': 'var(--semi-color-text-1)',
        'semi-color-text-2': 'var(--semi-color-text-2)',
        'semi-color-text-3': 'var(--semi-color-text-3)',
        'semi-color-link': 'var(--semi-color-link)',
      },
      borderRadius: {
        'semi-radius-small': 'var(--semi-border-radius-small)',
        'semi-radius-medium': 'var(--semi-border-radius-medium)',
        'semi-radius-large': 'var(--semi-border-radius-large)',
      },
      fontFamily: {
        sans: ['Lato', 'Helvetica Neue', 'Arial', 'Helvetica', 'Microsoft YaHei', 'sans-serif'],
      },
    },
  },
  plugins: [],
};
