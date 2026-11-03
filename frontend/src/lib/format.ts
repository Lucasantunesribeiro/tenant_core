const LOCALE = 'pt-BR'

const dateOnlyPattern = /^(\d{4})-(\d{2})-(\d{2})$/

/**
 * Datas sem horário ("2026-12-15", DateOnly da API) são interpretadas no fuso local.
 * `new Date('2026-12-15')` seria meia-noite UTC e apareceria como o dia anterior no Brasil.
 */
function parseDate(value: string) {
  const match = dateOnlyPattern.exec(value)
  if (match) {
    return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]))
  }
  return new Date(value)
}

export function formatDate(value: string | null | undefined) {
  if (!value) {
    return 'Sem data'
  }

  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  }).format(parseDate(value))
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return 'Sem atividade'
  }

  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parseDate(value))
}

export function formatRelativeUsage(current: number, limit: number) {
  if (!limit) {
    return 'Ilimitado'
  }

  return `${current}/${limit}`
}
export function initials(value: string) {
  return value
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}
