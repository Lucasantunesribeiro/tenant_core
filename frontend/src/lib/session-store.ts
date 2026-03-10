import type { SessionSnapshot } from '../types/api'

// Persisted to localStorage WITHOUT the access token (prevents XSS token exfiltration).
// accessToken lives only in memory and is lost on page reload — the Axios interceptor
// will call /api/auth/refresh (using the HttpOnly cookie) to obtain a new one silently.
const STORAGE_KEY = 'tenant-core.session'

type PersistedSession = Omit<SessionSnapshot, 'accessToken' | 'accessTokenExpiresAtUtc'>

type Listener = () => void

const listeners = new Set<Listener>()

function readInitialSession(): SessionSnapshot | null {
  if (typeof window === 'undefined') {
    return null
  }

  const raw = window.localStorage.getItem(STORAGE_KEY)
  if (!raw) {
    return null
  }

  try {
    const persisted = JSON.parse(raw) as PersistedSession
    // Reconstruct with empty token; interceptor will refresh before first API call
    return { ...persisted, accessToken: '', accessTokenExpiresAtUtc: new Date(0).toISOString() }
  } catch {
    window.localStorage.removeItem(STORAGE_KEY)
    return null
  }
}

let currentSession = readInitialSession()

function notify() {
  listeners.forEach((listener) => listener())
}

function persistSafe(snapshot: SessionSnapshot) {
  if (typeof window === 'undefined') return
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { accessToken, accessTokenExpiresAtUtc, ...safe } = snapshot
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(safe))
}

export const sessionStore = {
  getSnapshot: () => currentSession,
  subscribe(listener: Listener) {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
  set(snapshot: SessionSnapshot) {
    currentSession = snapshot
    persistSafe(snapshot)
    notify()
  },
  updateAccessToken(accessToken: string, accessTokenExpiresAtUtc: string) {
    if (!currentSession) {
      return
    }

    currentSession = {
      ...currentSession,
      accessToken,
      accessTokenExpiresAtUtc,
    }
    // No localStorage write — tokens stay in memory only
    notify()
  },
  clear() {
    currentSession = null
    if (typeof window !== 'undefined') {
      window.localStorage.removeItem(STORAGE_KEY)
    }
    notify()
  },
}
