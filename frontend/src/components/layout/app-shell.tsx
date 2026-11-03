import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
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
  const location = useLocation()
  const { session } = useSession()
  // O menu móvel guarda a rota em que foi aberto: trocar de rota já o fecha.
  const [menuOpenAt, setMenuOpenAt] = useState<string | null>(null)
  const menuOpen = menuOpenAt === location.pathname
  const setMenuOpen = (open: boolean | ((current: boolean) => boolean)) => {
    const next = typeof open === 'function' ? open(menuOpen) : open
    setMenuOpenAt(next ? location.pathname : null)
  }

  // Esc fecha o menu móvel.
  useEffect(() => {
    if (!menuOpen) {
      return
    }
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setMenuOpenAt(null)
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [menuOpen])

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
      <a
        href="#conteudo"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-[60] focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-semibold focus:text-primary focus:shadow"
      >
        Pular para o conteúdo
      </a>

      {/* Barra superior: só abaixo de md, onde a sidebar vira gaveta */}
      <header className="fixed inset-x-0 top-0 z-30 flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4 md:hidden">
        <div className="flex min-w-0 items-center gap-2">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary text-white">
            <span className="material-symbols-outlined text-lg" aria-hidden="true">deployed_code</span>
          </div>
          <div className="min-w-0">
            <p className="text-sm font-bold leading-tight text-slate-900">TenantCore</p>
            <p className="truncate text-xs text-slate-600">{session.tenantName}</p>
          </div>
        </div>
        <button
          type="button"
          onClick={() => setMenuOpen((open) => !open)}
          aria-expanded={menuOpen}
          aria-controls="menu-principal"
          aria-label={menuOpen ? 'Fechar menu' : 'Abrir menu'}
          className="flex h-10 w-10 items-center justify-center rounded-lg text-slate-700 hover:bg-slate-100"
        >
          <span className="material-symbols-outlined" aria-hidden="true">{menuOpen ? 'close' : 'menu'}</span>
        </button>
      </header>

      {menuOpen ? (
        <div className="fixed inset-0 z-30 bg-slate-950/40 md:hidden" aria-hidden="true" onClick={() => setMenuOpen(false)} />
      ) : null}

      <aside
        id="menu-principal"
        aria-label="Menu principal"
        className={[
          'fixed inset-y-0 left-0 z-40 w-72 max-w-[85vw] flex-col bg-white border-r border-slate-200 md:sticky md:top-0 md:z-auto md:flex md:h-screen md:w-64 md:max-w-none md:shrink-0',
          menuOpen ? 'flex' : 'hidden',
        ].join(' ')}
      >
        <div className="flex items-center gap-3 p-5 border-b border-slate-200">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-white">
            <span className="material-symbols-outlined text-xl">deployed_code</span>
          </div>
          <div>
            <h1 className="text-base font-bold leading-tight text-slate-900">TenantCore</h1>
            <p className="text-xs text-slate-600 truncate max-w-[140px]">{session.tenantName}</p>
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
              <span className="material-symbols-outlined text-[20px]" aria-hidden="true">{item.icon}</span>
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
            <span className="material-symbols-outlined text-[18px]" aria-hidden="true">logout</span>
            Sair
          </button>
        </div>
      </aside>

      <main id="conteudo" className="flex-1 min-w-0 pt-14 md:pt-0">
        <Outlet />
      </main>
    </div>
  )
}
