import type {
  ClientStatus,
  PlanCode,
  ProjectStatus,
  QuotaState,
  TaskPriority,
  UserRole,
  WorkTaskStatus,
} from '../types/api'

export const roleOptions: UserRole[] = ['Admin', 'Manager', 'User']
export const clientStatusOptions: ClientStatus[] = ['Lead', 'Active', 'Inactive']
export const projectStatusOptions: ProjectStatus[] = ['Planned', 'Active', 'Completed', 'Archived']
export const taskStatusOptions: WorkTaskStatus[] = ['Backlog', 'InProgress', 'Blocked', 'Done']
export const taskPriorityOptions: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical']
export const planOptions: PlanCode[] = ['Free', 'Pro', 'Business']

export function labelForQuotaState(value: QuotaState) {
  switch (value) {
    case 'Healthy':
      return 'Saudável'
    case 'NearLimit':
      return 'Próximo do limite'
    case 'Exceeded':
      return 'Cota excedida'
  }
}

export function labelForTaskStatus(value: WorkTaskStatus) {
  switch (value) {
    case 'Backlog':
      return 'Backlog'
    case 'InProgress':
      return 'Em progresso'
    case 'Blocked':
      return 'Bloqueado'
    case 'Done':
      return 'Concluído'
  }
}

export function labelForPriority(value: TaskPriority) {
  switch (value) {
    case 'Low':
      return 'Baixa'
    case 'Medium':
      return 'Média'
    case 'High':
      return 'Alta'
    case 'Critical':
      return 'Crítica'
  }
}
