import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { LoginPage } from './login-page'
import { renderWithProviders } from '../../test/render'
import { sessionStore } from '../../lib/session-store'

const { loginRequestMock } = vi.hoisted(() => ({
  loginRequestMock: vi.fn(),
}))

vi.mock('../../lib/http', async () => {
  const actual = await vi.importActual<typeof import('../../lib/http')>('../../lib/http')
  return {
    ...actual,
    loginRequest: loginRequestMock,
  }
})

describe('LoginPage', () => {
  beforeEach(() => {
    loginRequestMock.mockResolvedValue({
      accessToken: 'token',
      accessTokenExpiresAtUtc: new Date().toISOString(),
      user: {
        id: '1',
        tenantId: '11111111-1111-1111-1111-111111111111',
        email: 'admin@acme.test',
        fullName: 'Ava Stone',
        role: 'Admin',
      },
      tenantName: 'Acme Operations',
      planCode: 'Business',
    })
  })

  afterEach(() => {
    sessionStore.clear()
    vi.clearAllMocks()
  })

  it('submits seeded credentials and navigates after success', async () => {
    const user = userEvent.setup()

    renderWithProviders(
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<div>Dashboard landing</div>} />
      </Routes>,
      {
        route: '/login',
      },
    )

    await user.clear(screen.getByLabelText('Email'))
    await user.type(screen.getByLabelText('Email'), 'admin@globex.test')
    await user.clear(screen.getByLabelText('Password'))
    await user.type(screen.getByLabelText('Password'), 'Passw0rd!')
    await user.clear(screen.getByPlaceholderText('11111111-1111-1111-1111-111111111111'))
    await user.type(screen.getByPlaceholderText('11111111-1111-1111-1111-111111111111'), '22222222-2222-2222-2222-222222222222')
    await user.click(screen.getByRole('button', { name: 'Enter workspace' }))

    await waitFor(() => {
      expect(loginRequestMock).toHaveBeenCalledWith(
        'admin@globex.test',
        'Passw0rd!',
        '22222222-2222-2222-2222-222222222222',
      )
    })

    expect(await screen.findByText('Dashboard landing')).toBeInTheDocument()
  })
})
