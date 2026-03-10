export function formatDate(value: string | null | undefined) {
  if (!value) {
    return 'No date'
  }

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date(value))
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) {
    return 'No activity'
  }

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value))
}

export function formatRelativeUsage(current: number, limit: number) {
  if (!limit) {
    return 'Unlimited'
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
