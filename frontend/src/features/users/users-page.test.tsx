import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { UsersPage } from './users-page'
import { renderWithProviders } from '../../test/render'
import { sessionStore } from '../../lib/session-store'

const { listMock, createMock, changeRoleMock } = vi.hoisted(() => ({
  listMock: vi.fn(),
  createMock: vi.fn(),
  changeRoleMock: vi.fn(),
}))

vi.mock('../../lib/api', () => ({
  queryKeys: {
    users: (search = '', role = '') => ['users', search, role],
  },
  usersApi: {
    list: listMock,
    create: createMock,
    changeRole: changeRoleMock,
  },
}))

vi.mock('../../lib/http', async () => {
  const actual = await vi.importActual<typeof import('../../lib/http')>('../../lib/http')
  return {
    ...actual,
    problemMessage: () => 'Plan limit reached for the Free plan.',
  }
})

describe('UsersPage', () => {
  beforeEach(() => {
    sessionStore.set({
      accessToken: 'token',
      accessTokenExpiresAtUtc: new Date().toISOString(),
      tenantName: 'Acme Operations',
      planCode: 'Free',
      user: {
        id: 'aaaa1111-1111-1111-1111-111111111111',
        tenantId: '11111111-1111-1111-1111-111111111111',
        email: 'admin@acme.test',
        fullName: 'Ava Stone',
        role: 'Admin',
      },
    })

    listMock.mockResolvedValue({
      items: [
        {
          id: 'aaaa1111-1111-1111-1111-111111111111',
          email: 'admin@acme.test',
          fullName: 'Ava Stone',
          role: 'Admin',
          invitationPending: false,
          lastLoginAtUtc: new Date().toISOString(),
        },
      ],
      page: 1,
      pageSize: 50,
      totalCount: 1,
      totalPages: 1,
    })
    createMock.mockRejectedValue(new Error('plan-limit'))
  })

  afterEach(() => {
    sessionStore.clear()
    vi.clearAllMocks()
  })

  it('shows plan-limit feedback when user creation fails', async () => {
    const user = userEvent.setup()
    renderWithProviders(<UsersPage />)

    await screen.findByText('Ava Stone')
    await user.click(screen.getByRole('button', { name: 'Convidar usuário' }))
    await user.type(screen.getByLabelText('Nome completo'), 'Lena Cole')
    await user.type(screen.getByLabelText('E-mail'), 'lena@acme.test')
    await user.clear(screen.getByLabelText('Senha temporária'))
    await user.type(screen.getByLabelText('Senha temporária'), 'Passw0rd!')
    await user.click(screen.getByRole('button', { name: 'Criar usuário' }))
    const submission = createMock.mock.results[0]?.value

    if (submission && typeof submission.then === 'function') {
      await act(async () => {
        await submission.catch(() => undefined)
      })
    }

    await waitFor(() => {
      expect(createMock).toHaveBeenCalled()
    })

    expect(await screen.findByText('Plan limit reached for the Free plan.')).toBeInTheDocument()
    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Criar usuário' })).toBeEnabled()
    })
  })
})
