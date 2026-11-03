import { describe, expect, it } from 'vitest'
import { formatDate, formatRelativeUsage } from './format'

describe('formatDate', () => {
  it('keeps date-only values on the same calendar day regardless of the time zone', () => {
    // DateOnly da API; antes virava meia-noite UTC e aparecia como 14/12 em fusos negativos.
    expect(formatDate('2026-12-15')).toMatch(/^15 de dez\.? de 2026$/)
  })

  it('uses Portuguese fallbacks', () => {
    expect(formatDate(null)).toBe('Sem data')
    expect(formatRelativeUsage(3, 0)).toBe('Ilimitado')
    expect(formatRelativeUsage(3, 15)).toBe('3/15')
  })
})
