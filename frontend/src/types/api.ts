export type UserRole = 'Admin' | 'Manager' | 'User'
export type ClientStatus = 'Lead' | 'Active' | 'Inactive'
export type ProjectStatus = 'Planned' | 'Active' | 'Completed' | 'Archived'
export type WorkTaskStatus = 'Backlog' | 'InProgress' | 'Blocked' | 'Done'
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical'
export type PlanCode = 'Free' | 'Pro' | 'Business'
export type QuotaState = 'Healthy' | 'NearLimit' | 'Exceeded'

export interface AuthenticatedUserDto {
  id: string
  tenantId: string
  email: string
  fullName: string
  role: UserRole
}

export interface LoginEnvelope {
  accessToken: string
  accessTokenExpiresAtUtc: string
  user: AuthenticatedUserDto
  tenantName: string
  planCode: PlanCode
}

export interface RefreshEnvelope {
  accessToken: string
  accessTokenExpiresAtUtc: string
}

export type CurrentUserResponse = AuthenticatedUserDto

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface UsageOverview {
  activeUsers: number
  projects: number
  tasks: number
  clients: number
  lastSnapshotAtUtc: string | null
}

export interface TenantPlanSummary {
  planCode: PlanCode
  planName: string
  maxUsers: number
  maxProjects: number
  maxClients: number
  quotaState: QuotaState
  warningMessage: string | null
}

export interface UsageDashboardResponse {
  usage: UsageOverview
  plan: TenantPlanSummary
}

export interface TenantProfileResponse {
  id: string
  name: string
  slug: string
  billingEmail: string
  supportEmail: string
  timeZone: string
  theme: string
  allowedDomains: string
}

export interface SubscriptionResponse {
  planCode: PlanCode
  planName: string
  description: string
  maxUsers: number
  maxProjects: number
  maxClients: number
  quotaState: QuotaState
  warningMessage: string | null
  renewedAtUtc: string
  nextRenewalAtUtc: string
}

export interface UserListItem {
  id: string
  email: string
  fullName: string
  role: UserRole
  invitationPending: boolean
  lastLoginAtUtc: string | null
}

export interface ClientListItem {
  id: string
  name: string
  email: string
  contactName: string
  status: ClientStatus
  notes: string
  updatedAtUtc: string
}

export interface ProjectListItem {
  id: string
  name: string
  code: string
  status: ProjectStatus
  clientId: string | null
  clientName: string | null
  ownerUserId: string | null
  ownerName: string | null
  dueDate: string | null
  taskCount: number
  updatedAtUtc: string
}

export interface TaskListItem {
  id: string
  projectId: string
  title: string
  description: string
  status: WorkTaskStatus
  priority: TaskPriority
  projectName: string
  assigneeUserId: string | null
  assigneeName: string | null
  dueDate: string | null
  updatedAtUtc: string
}

export interface AuditLogItem {
  id: string
  actorUserId: string | null
  action: string
  entityType: string
  entityId: string
  correlationId: string
  metadataJson: string
  occurredAtUtc: string
}

export type SessionSnapshot = LoginEnvelope

export interface ProblemDetailsPayload {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  errors?: Record<string, string[]>
}
