import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { sessionStore } from './session-store'
import type { LoginEnvelope, ProblemDetailsPayload, RefreshEnvelope } from '../types/api'

const API_BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000'

let refreshRequest: Promise<RefreshEnvelope | null> | null = null

type RetriableConfig = InternalAxiosRequestConfig & {
  _retry?: boolean
}

export const api = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
})

async function refreshSession(tenantId: string) {
  const response = await axios.post<RefreshEnvelope>(
    `${API_BASE_URL}/api/auth/refresh`,
    {},
    {
      withCredentials: true,
      headers: {
        'X-Tenant-Id': tenantId,
      },
    },
  )

  sessionStore.updateAccessToken(response.data.accessToken, response.data.accessTokenExpiresAtUtc)
  return response.data
}

api.interceptors.request.use((config) => {
  const session = sessionStore.getSnapshot()
  if (session?.accessToken) {
    config.headers.Authorization = `Bearer ${session.accessToken}`
  }
  if (session?.user.tenantId) {
    config.headers['X-Tenant-Id'] = session.user.tenantId
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ProblemDetailsPayload>) => {
    const config = error.config as RetriableConfig | undefined
    const session = sessionStore.getSnapshot()

    if (
      error.response?.status !== 401 ||
      !config ||
      config._retry ||
      config.url?.includes('/api/auth/login') ||
      config.url?.includes('/api/auth/refresh') ||
      !session?.user.tenantId
    ) {
      throw error
    }

    config._retry = true
    refreshRequest ??= refreshSession(session.user.tenantId).finally(() => {
      refreshRequest = null
    })

    try {
      const refreshed = await refreshRequest
      if (!refreshed) {
        sessionStore.clear()
        throw error
      }

      config.headers.Authorization = `Bearer ${refreshed.accessToken}`
      return api(config)
    } catch {
      sessionStore.clear()
      throw error
    }
  },
)

/**
 * Restaura o access token depois de um reload: a sessão (sem token) vem do localStorage
 * e o token é obtido de novo pelo cookie HttpOnly de refresh. Se o refresh falhar a
 * sessão é descartada e o usuário volta para o login.
 */
export async function restoreSession(): Promise<boolean> {
  const session = sessionStore.getSnapshot()
  if (!session) {
    return false
  }
  if (session.accessToken) {
    return true
  }

  refreshRequest ??= refreshSession(session.user.tenantId).finally(() => {
    refreshRequest = null
  })

  try {
    await refreshRequest
    return true
  } catch {
    sessionStore.clear()
    return false
  }
}

export const API_UNREACHABLE_MESSAGE =
  'Não foi possível falar com a API. Na demo gratuita o servidor hiberna quando fica ocioso: aguarde cerca de 1 minuto e tente de novo.'

export function problemMessage(error: unknown) {
  if (axios.isAxiosError<ProblemDetailsPayload>(error)) {
    if (!error.response) {
      return API_UNREACHABLE_MESSAGE
    }
    if (error.response.status === 429) {
      return 'Muitas tentativas em pouco tempo. Aguarde um minuto e tente de novo.'
    }
    return error.response.data?.detail ?? error.response.data?.title ?? `A requisição falhou (HTTP ${error.response.status}).`
  }

  return 'Erro inesperado.'
}

export async function loginRequest(email: string, password: string, tenantId: string) {
  const response = await api.post<LoginEnvelope>(
    '/api/auth/login',
    { email, password },
    {
      headers: {
        'X-Tenant-Id': tenantId,
      },
    },
  )

  sessionStore.set(response.data)
  return response.data
}

export async function logoutRequest() {
  try {
    await api.post('/api/auth/logout')
  } finally {
    sessionStore.clear()
  }
}
