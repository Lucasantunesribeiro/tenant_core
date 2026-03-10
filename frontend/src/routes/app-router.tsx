import { createBrowserRouter } from 'react-router-dom'
import { ProtectedRoute } from '../features/auth/protected-route'
import { LoginPage } from '../features/auth/login-page'
import { DashboardPage } from '../features/dashboard/dashboard-page'
import { ProjectsPage } from '../features/projects/projects-page'
import { TasksPage } from '../features/tasks/tasks-page'
import { ClientsPage } from '../features/clients/clients-page'
import { UsersPage } from '../features/users/users-page'
import { BillingPage } from '../features/billing/billing-page'
import { AuditPage } from '../features/audit/audit-page'
import { SettingsPage } from '../features/settings/settings-page'
import { AppShell } from '../components/layout/app-shell'

export const appRouter = createBrowserRouter(
  [
    {
      path: '/login',
      element: <LoginPage />,
    },
    {
      element: <ProtectedRoute />,
      children: [
        {
          element: <AppShell />,
          children: [
            { index: true, element: <DashboardPage /> },
            { path: 'projects', element: <ProjectsPage /> },
            { path: 'tasks', element: <TasksPage /> },
            { path: 'clients', element: <ClientsPage /> },
            { path: 'users', element: <UsersPage /> },
            { path: 'billing', element: <BillingPage /> },
            { path: 'audit', element: <AuditPage /> },
            { path: 'settings', element: <SettingsPage /> },
          ],
        },
      ],
    },
  ],
  {
    future: {
      v7_relativeSplatPath: true,
    },
  },
)
