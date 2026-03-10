import { useSyncExternalStore } from 'react'
import { sessionStore } from '../../lib/session-store'

export function useSession() {
  const session = useSyncExternalStore(
    sessionStore.subscribe,
    sessionStore.getSnapshot,
    sessionStore.getSnapshot,
  )

  return {
    session,
    isAuthenticated: Boolean(session?.accessToken),
    isAdmin: session?.user.role === 'Admin',
    isManager: session?.user.role === 'Manager',
    canManageWorkspace: session?.user.role === 'Admin' || session?.user.role === 'Manager',
  }
}
