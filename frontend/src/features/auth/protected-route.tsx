import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useSession } from './use-session'
import type { UserRole } from '../../types/api'

interface ProtectedRouteProps {
  roles?: UserRole[]
}

export function ProtectedRoute({ roles }: ProtectedRouteProps) {
  const location = useLocation()
  const { session } = useSession()

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  if (roles && !roles.includes(session.user.role)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}
