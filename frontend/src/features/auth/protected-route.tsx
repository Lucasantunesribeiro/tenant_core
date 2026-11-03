import { useEffect } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { restoreSession } from '../../lib/http'
import { FullPageLoader } from '../../components/ui/primitives'
import { useSession } from './use-session'
import type { UserRole } from '../../types/api'

interface ProtectedRouteProps {
  roles?: UserRole[]
}

export function ProtectedRoute({ roles }: ProtectedRouteProps) {
  const location = useLocation()
  const { session, isInitializing } = useSession()

  // Após um reload só a identidade volta do localStorage; o token é renovado aqui pelo
  // cookie de refresh. Sem esse disparo a rota ficava em branco para sempre, porque
  // nenhuma chamada à API acontecia para acionar o refresh do interceptor.
  useEffect(() => {
    if (isInitializing) {
      void restoreSession()
    }
  }, [isInitializing])

  // No session at all — definitely unauthenticated; redirect to login.
  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  // Session restored from localStorage but the access token is not yet in
  // memory (restoreSession() above fetches it via the refresh cookie).
  // Hold the protected tree until the token is ready so downstream components that
  // depend on an active token don't flash a broken state or fire unauthenticated requests.
  if (isInitializing) {
    return <FullPageLoader label="Restaurando sua sessão..." />
  }

  if (roles && !roles.includes(session.user.role)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
