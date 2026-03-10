import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { logoutRequest } from '../../lib/http'
import { initials } from '../../lib/format'
import { useSession } from '../../features/auth/use-session'

type NavigationItem = {
  to: string
  label: string
  icon: string
  roles?: Array<'Admin' | 'Manager' | 'User'>
}

const navigation: NavigationItem[] = [
  { to: '/', label: 'Visão Geral', icon: 'dashboard' },
  { to: '/projects', label: 'Projetos', icon: 'folder_open' },
  { to: '/tasks', label: 'Tarefas', icon: 'task_alt' },
  { to: '/clients', label: 'Clientes', icon: 'groups' },
  { to: '/users', label: 'Usuários & Funções', icon: 'manage_accounts', roles: ['Admin'] },
  { to: '/billing', label: 'Faturamento', icon: 'payments' },
  { to: '/audit', label: 'Logs de Auditoria', icon: 'manage_search', roles: ['Admin'] },
  { to: '/settings', label: 'Configurações', icon: 'settings' },
]

export function AppShell() {
  const navigate = useNavigate()
  const { session } = useSession()

  if (!session) {
    return null
  }

  const visibleNavigation = navigation.filter((item) => !item.roles || item.roles.includes(session.user.role))

  async function handleLogout() {
    await logoutRequest()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-screen bg-background-light font-display">
      <aside className="hidden md:flex w-64 shrink-0 flex-col bg-white border-r border-slate-200 sticky top-0 h-screen">
        <div className="flex items-center gap-3 p-5 border-b border-slate-200">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-white">
            <span className="material-symbols-outlined text-xl">deployed_code</span>
          </div>
          <div>
            <h1 className="text-base font-bold leading-tight text-slate-900">TenantCore</h1>
            <p className="text-xs text-slate-500 truncate max-w-[140px]">{session.tenantName}</p>
          </div>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto px-4 py-4">
          {visibleNavigation.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              className={({ isActive }) =>
                [
                  'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary/10 text-primary font-semibold'
                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                ].join(' ')
              }
            >
              <span className="material-symbols-outlined text-[20px]">{item.icon}</span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="border-t border-slate-200 p-4">
          <div className="mb-3 flex items-center gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
              {initials(session.user.fullName)}
            </div>
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-slate-900">{session.user.fullName}</p>
              <p className="truncate text-xs text-slate-500">{session.user.email}</p>
            </div>
          </div>
          <button
            onClick={handleLogout}
            className="flex w-full items-center justify-center gap-2 rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-50"
          >
            <span className="material-symbols-outlined text-[18px]">logout</span>
            Sair
          </button>
        </div>
      </aside>

      <main className="flex-1 min-w-0">
        <Outlet />
      </main>
    </div>
  )
}
