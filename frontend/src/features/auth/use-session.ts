import { useSyncExternalStore } from 'react'
import { sessionStore } from '../../lib/session-store'

export function useSession() {
  const session = useSyncExternalStore(
    sessionStore.subscribe,
    sessionStore.getSnapshot,
    sessionStore.getSnapshot,
  )

  // A session exists but has an empty access token when the page reloads: the
  // user identity is restored from localStorage but the token lives only in
  // memory and must be re-acquired via the silent /api/auth/refresh call that
  // the Axios response interceptor fires on the first 401. During this window
  // the user IS considered authenticated (session != null) but the token is
  // not yet ready — callers that need a live token should check !isInitializing.
  const isAuthenticated = session !== null
  const isInitializing = session !== null && session.accessToken === ''

  return {
    session,
    isAuthenticated,
    isInitializing,
    isAdmin: session?.user.role === 'Admin',
    isManager: session?.user.role === 'Manager',
    canManageWorkspace: session?.user.role === 'Admin' || session?.user.role === 'Manager',
  }
}
