import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { API_UNREACHABLE_MESSAGE, problemMessage } from './http'

function axiosError(status?: number, data?: unknown) {
  const config = { headers: new AxiosHeaders() }
  const response = status
    ? { status, statusText: '', headers: {}, config, data }
    : undefined
  return new AxiosError('failed', status ? 'ERR_BAD_RESPONSE' : 'ERR_NETWORK', config, null, response)
}

describe('problemMessage', () => {
  it('explains that the free-tier API may be waking up when there is no response', () => {
    expect(problemMessage(axiosError())).toBe(API_UNREACHABLE_MESSAGE)
  })

  it('prefers the ProblemDetails detail returned by the API', () => {
    expect(problemMessage(axiosError(409, { title: 'Conflict', detail: 'Plan limit reached.' }))).toBe('Plan limit reached.')
  })

  it('falls back to a Portuguese message with the HTTP status', () => {
    expect(problemMessage(axiosError(500, {}))).toBe('A requisição falhou (HTTP 500).')
    expect(problemMessage(axiosError(429, {}))).toMatch(/Muitas tentativas/)
    expect(problemMessage(new Error('boom'))).toBe('Erro inesperado.')
  })
})
