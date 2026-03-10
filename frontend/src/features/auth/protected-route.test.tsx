import { screen } from '@testing-library/react'
import { Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'
import { ProtectedRoute } from './protected-route'
import { sessionStore } from '../../lib/session-store'
import { renderWithProviders } from '../../test/render'

describe('ProtectedRoute', () => {
  afterEach(() => {
    sessionStore.clear()
  })

  it('redirects anonymous users to the login route', () => {
    renderWithProviders(
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

    expect(screen.getByText('Login screen')).toBeInTheDocument()
  })
})
