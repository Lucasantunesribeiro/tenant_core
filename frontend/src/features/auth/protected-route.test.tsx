import { act, screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ProtectedRoute } from './protected-route'
import { sessionStore } from '../../lib/session-store'
import { renderWithProviders } from '../../test/render'

const { restoreSessionMock } = vi.hoisted(() => ({
  restoreSessionMock: vi.fn(),
}))

vi.mock('../../lib/http', async () => {
  const actual = await vi.importActual<typeof import('../../lib/http')>('../../lib/http')
  return {
    ...actual,
    restoreSession: restoreSessionMock,
  }
})

const persistedSession = {
  accessToken: '',
  accessTokenExpiresAtUtc: new Date(0).toISOString(),
  user: {
    id: '1',
    tenantId: '11111111-1111-1111-1111-111111111111',
    email: 'admin@acme.test',
    fullName: 'Ava Stone',
    role: 'Admin' as const,
  },
  tenantName: 'Acme Operations',
  planCode: 'Pro' as const,
}

function renderProtected() {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<div>Login screen</div>} />
      <Route element={<ProtectedRoute />}>
        <Route path="/projects" element={<div>Projects screen</div>} />
      </Route>
    </Routes>,
    {
      route: '/projects',
    },
  )
}

describe('ProtectedRoute', () => {
  afterEach(() => {
    sessionStore.clear()
    vi.clearAllMocks()
  })

  it('redirects anonymous users to the login route', () => {
    renderProtected()

    expect(screen.getByText('Login screen')).toBeInTheDocument()
    expect(restoreSessionMock).not.toHaveBeenCalled()
  })

  it('restores the access token after a reload and then renders the route', async () => {
    // Sessão reidratada do localStorage: identidade presente, token vazio.
    sessionStore.set(persistedSession)
    let finishRefresh: () => void = () => {}
    restoreSessionMock.mockImplementation(
      () =>
        new Promise<boolean>((resolve) => {
          finishRefresh = () => {
            sessionStore.updateAccessToken('fresh-token', new Date(Date.now() + 60_000).toISOString())
            resolve(true)
          }
        }),
    )

    renderProtected()

    expect(screen.getByRole('status')).toHaveTextContent('Restaurando sua sessão')
    act(() => finishRefresh())
    expect(await screen.findByText('Projects screen')).toBeInTheDocument()
    expect(restoreSessionMock).toHaveBeenCalledTimes(1)
  })

  it('sends the user to login when the refresh cookie is no longer valid', async () => {
    sessionStore.set(persistedSession)
    restoreSessionMock.mockImplementation(async () => {
      sessionStore.clear()
      return false
    })

    renderProtected()

    expect(await screen.findByText('Login screen')).toBeInTheDocument()
  })
})
