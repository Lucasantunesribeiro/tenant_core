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

export function problemMessage(error: unknown) {
  if (axios.isAxiosError<ProblemDetailsPayload>(error)) {
    return error.response?.data?.detail ?? error.response?.data?.title ?? 'Request failed.'
  }

  return 'Unexpected error.'
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
