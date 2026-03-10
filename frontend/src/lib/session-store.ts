import type { SessionSnapshot } from '../types/api'

const STORAGE_KEY = 'tenant-core.session'

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
    return JSON.parse(raw) as SessionSnapshot
  } catch {
    window.localStorage.removeItem(STORAGE_KEY)
    return null
  }
}

let currentSession = readInitialSession()

function notify() {
  listeners.forEach((listener) => listener())
}

export const sessionStore = {
  getSnapshot: () => currentSession,
  subscribe(listener: Listener) {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
  set(snapshot: SessionSnapshot) {
    currentSession = snapshot
    if (typeof window !== 'undefined') {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(snapshot))
    }
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

    if (typeof window !== 'undefined') {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(currentSession))
    }
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
