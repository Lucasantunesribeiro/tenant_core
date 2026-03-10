import { api } from './http'
import type {
  AuditLogItem,
  ClientListItem,
  CurrentUserResponse,
  PagedResult,
  ProjectListItem,
  SubscriptionResponse,
  TaskListItem,
  TenantProfileResponse,
  UsageDashboardResponse,
  UserListItem,
  UserRole,
  ClientStatus,
  ProjectStatus,
  TaskPriority,
  WorkTaskStatus,
  PlanCode,
} from '../types/api'

export const queryKeys = {
  me: ['me'] as const,
  usage: ['usage'] as const,
  subscription: ['subscription'] as const,
  tenantProfile: ['tenant-profile'] as const,
  users: (search = '', role = '') => ['users', search, role] as const,
  clients: (search = '', status = '') => ['clients', search, status] as const,
  projects: (search = '', status = '') => ['projects', search, status] as const,
  tasks: (search = '', status = '', projectId = '') => ['tasks', search, status, projectId] as const,
  auditLogs: (action = '', entityType = '') => ['audit-logs', action, entityType] as const,
}

export const tenantApi = {
  async getProfile() {
    const { data } = await api.get<TenantProfileResponse>('/api/tenant/profile')
    return data
  },
  async updateSettings(payload: {
    name: string
    billingEmail: string
    supportEmail: string
    timeZone: string
    theme: string
    allowedDomains: string
  }) {
    await api.put('/api/tenant/settings', payload)
  },
  async getSubscription() {
    const { data } = await api.get<SubscriptionResponse>('/api/tenant/subscription')
    return data
  },
  async changePlan(planCode: PlanCode) {
    await api.post('/api/tenant/subscription/change-plan', { planCode })
  },
  async getUsage() {
    const { data } = await api.get<UsageDashboardResponse>('/api/tenant/usage')
    return data
  },
}

export const authApi = {
  async getCurrentUser() {
    const { data } = await api.get<CurrentUserResponse>('/api/auth/me')
    return data
  },
}

export const usersApi = {
  async list(search = '', role = '') {
    const { data } = await api.get<PagedResult<UserListItem>>('/api/users', {
      params: { search: search || undefined, role: role || undefined, page: 1, pageSize: 50 },
    })
    return data
  },
  async create(payload: { email: string; fullName: string; password: string; role: UserRole; invitationPending: boolean }) {
    await api.post('/api/users', payload)
  },
  async changeRole(userId: string, role: UserRole) {
    await api.patch(`/api/users/${userId}/role`, { role })
  },
}

export const clientsApi = {
  async list(search = '', status = '') {
    const { data } = await api.get<PagedResult<ClientListItem>>('/api/clients', {
      params: { search: search || undefined, status: status || undefined, page: 1, pageSize: 50 },
    })
    return data
  },
  async create(payload: { name: string; email: string; contactName: string; status: ClientStatus; notes: string }) {
    await api.post('/api/clients', payload)
  },
  async update(clientId: string, payload: { name: string; email: string; contactName: string; status: ClientStatus; notes: string }) {
    await api.put(`/api/clients/${clientId}`, payload)
  },
  async remove(clientId: string) {
    await api.delete(`/api/clients/${clientId}`)
  },
}

export const projectsApi = {
  async list(search = '', status = '') {
    const { data } = await api.get<PagedResult<ProjectListItem>>('/api/projects', {
      params: { search: search || undefined, status: status || undefined, page: 1, pageSize: 50 },
    })
    return data
  },
  async create(payload: {
    clientId: string | null
    ownerUserId: string | null
    name: string
    code: string
    description: string
    status: ProjectStatus
    startDate: string | null
    dueDate: string | null
  }) {
    await api.post('/api/projects', payload)
  },
  async update(projectId: string, payload: {
    clientId: string | null
    ownerUserId: string | null
    name: string
    code: string
    description: string
    status: ProjectStatus
    startDate: string | null
    dueDate: string | null
  }) {
    await api.put(`/api/projects/${projectId}`, payload)
  },
  async remove(projectId: string) {
    await api.delete(`/api/projects/${projectId}`)
  },
}

export const tasksApi = {
  async list(search = '', status = '', projectId = '') {
    const { data } = await api.get<PagedResult<TaskListItem>>('/api/tasks', {
      params: { search: search || undefined, status: status || undefined, projectId: projectId || undefined, page: 1, pageSize: 50 },
    })
    return data
  },
  async create(payload: {
    projectId: string
    assigneeUserId: string | null
    title: string
    description: string
    status: WorkTaskStatus
    priority: TaskPriority
    dueDate: string | null
  }) {
    await api.post('/api/tasks', payload)
  },
  async update(taskId: string, payload: {
    projectId: string
    assigneeUserId: string | null
    title: string
    description: string
    status: WorkTaskStatus
    priority: TaskPriority
    dueDate: string | null
  }) {
    await api.put(`/api/tasks/${taskId}`, payload)
  },
  async updateStatus(taskId: string, status: WorkTaskStatus) {
    await api.patch(`/api/tasks/${taskId}/status`, { status })
  },
  async remove(taskId: string) {
    await api.delete(`/api/tasks/${taskId}`)
  },
}

export const auditApi = {
  async list(action = '', entityType = '') {
    const { data } = await api.get<PagedResult<AuditLogItem>>('/api/audit-logs', {
      params: { action: action || undefined, entityType: entityType || undefined, page: 1, pageSize: 100 },
    })
    return data
  },
}
