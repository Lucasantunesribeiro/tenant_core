import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useSession } from './use-session'
import type { UserRole } from '../../types/api'

interface ProtectedRouteProps {
  roles?: UserRole[]
}

export function ProtectedRoute({ roles }: ProtectedRouteProps) {
  const location = useLocation()
  const { session, isInitializing } = useSession()

  // No session at all — definitely unauthenticated; redirect to login.
  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  // Session restored from localStorage but the access token is not yet in
  // memory (it will be fetched via silent refresh on the first API call).
  // Render nothing until the token is ready so downstream components that
  // depend on an active token don't flash a broken state or fire unauthenticated requests.
  if (isInitializing) {
    return null
  }

  if (roles && !roles.includes(session.user.role)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
