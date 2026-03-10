import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { projectsApi, queryKeys, tasksApi, usersApi } from '../../lib/api'
import { formatDate, formatDateTime } from '../../lib/format'
import { labelForPriority, labelForTaskStatus, taskPriorityOptions, taskStatusOptions } from '../../lib/lookups'
import { problemMessage } from '../../lib/http'
import type { TaskListItem, TaskPriority, WorkTaskStatus } from '../../types/api'
import { useSession } from '../auth/use-session'
import { Button, EmptyState, ErrorState, Field, Input, LoadingState, Modal, Select, Textarea } from '../../components/ui/primitives'

type TaskDraft = {
  projectId: string
  assigneeUserId: string | null
  title: string
  description: string
  status: WorkTaskStatus
  priority: TaskPriority
  dueDate: string | null
}

const emptyDraft: TaskDraft = {
  projectId: '', assigneeUserId: null, title: '', description: '', status: 'Backlog', priority: 'Medium', dueDate: null,
}

const statusBadgeClass: Record<WorkTaskStatus, string> = {
  Backlog: 'bg-slate-100 text-slate-600 border-slate-200',
  InProgress: 'bg-blue-100 text-blue-700 border-blue-200',
  Blocked: 'bg-red-100 text-red-700 border-red-200',
  Done: 'bg-emerald-100 text-emerald-700 border-emerald-200',
}

const priorityBadgeClass: Record<TaskPriority, string> = {
  Low: 'bg-slate-100 text-slate-500',
  Medium: 'bg-yellow-100 text-yellow-700',
  High: 'bg-orange-100 text-orange-700',
  Critical: 'bg-red-100 text-red-700',
}

export function TasksPage() {
  const queryClient = useQueryClient()
  const { canManageWorkspace } = useSession()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [projectId, setProjectId] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [selectedTask, setSelectedTask] = useState<TaskListItem | null>(null)
  const [draft, setDraft] = useState<TaskDraft>(emptyDraft)

  const tasksQuery = useQuery({ queryKey: queryKeys.tasks(search, status, projectId), queryFn: () => tasksApi.list(search, status, projectId) })
  const projectsQuery = useQuery({ queryKey: queryKeys.projects(), queryFn: () => projectsApi.list() })
  const usersQuery = useQuery({ queryKey: queryKeys.users(), queryFn: () => usersApi.list(), enabled: canManageWorkspace })

  const saveMutation = useMutation({
    mutationFn: async (payload: TaskDraft) => {
      if (selectedTask) { await tasksApi.update(selectedTask.id, payload); return }
      await tasksApi.create(payload)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['tasks'] })
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
      setSelectedTask(null); setDraft(emptyDraft); setModalOpen(false)
    },
  })

  const statusMutation = useMutation({
    mutationFn: async ({ taskId, nextStatus }: { taskId: string; nextStatus: WorkTaskStatus }) => tasksApi.updateStatus(taskId, nextStatus),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['tasks'] }) },
  })

  const deleteMutation = useMutation({
    mutationFn: tasksApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['tasks'] })
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
    },
  })

  const rows = useMemo(() => tasksQuery.data?.items ?? [], [tasksQuery.data])
  const projectOptions = projectsQuery.data?.items ?? []
  const userOptions = usersQuery.data?.items ?? []

  function openCreate() {
    setSelectedTask(null)
    setDraft({ ...emptyDraft, projectId: projectOptions[0]?.id ?? '' })
    setModalOpen(true)
  }

  function openEdit(task: TaskListItem) {
    setSelectedTask(task)
    setDraft({ projectId: task.projectId, assigneeUserId: task.assigneeUserId, title: task.title, description: task.description, status: task.status, priority: task.priority, dueDate: task.dueDate })
    setModalOpen(true)
  }

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Camada de Execução</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Tarefas</h1>
          <p className="mt-1 text-sm text-slate-500">Gerencie tarefas com atribuições, prioridades e transições de status.</p>
        </div>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => { setSearch(''); setStatus(''); setProjectId('') }}>
            <span className="material-symbols-outlined text-[18px] mr-1">filter_list_off</span>
            Limpar
          </Button>
          {canManageWorkspace ? (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" />
              Nova Tarefa
            </Button>
          ) : null}
        </div>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <div className="relative max-w-sm flex-1">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-xl">search</span>
          <input
            type="text"
            placeholder="Pesquisar tarefas..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <select
          value={status}
          onChange={(e) => setStatus(e.target.value)}
          className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary"
        >
          <option value="">Todos os status</option>
          {taskStatusOptions.map((opt) => <option key={opt} value={opt}>{labelForTaskStatus(opt)}</option>)}
        </select>
        <select
          value={projectId}
          onChange={(e) => setProjectId(e.target.value)}
          className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary"
        >
          <option value="">Todos os projetos</option>
          {projectOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
      </div>

      {tasksQuery.isLoading ? <LoadingState label="Carregando tarefas..." /> : null}
      {tasksQuery.isError ? <ErrorState detail="Os dados de tarefas não puderam ser carregados." retry={() => tasksQuery.refetch()} /> : null}
      {!tasksQuery.isLoading && !tasksQuery.isError && rows.length === 0 ? (
        <EmptyState
          title="Nenhuma tarefa encontrada"
          description="Crie tarefas para demonstrar ciclos de vida, atribuições e filtragem segura por tenant."
          action={canManageWorkspace ? <Button onClick={openCreate}>Criar tarefa</Button> : undefined}
        />
      ) : null}

      {!tasksQuery.isLoading && !tasksQuery.isError && rows.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50">
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Tarefa</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Status</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Prioridade</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Projeto</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Responsável</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Prazo</th>
                  {canManageWorkspace ? <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500 text-right">Ações</th> : null}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((task) => (
                  <tr key={task.id} className="transition-colors hover:bg-slate-50/60">
                    <td className="px-6 py-4">
                      <p className="font-bold text-slate-900">{task.title}</p>
                      <p className="text-xs text-slate-500 mt-0.5">{task.description || 'Sem descrição'} · {formatDateTime(task.updatedAtUtc)}</p>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${statusBadgeClass[task.status]}`}>
                        {labelForTaskStatus(task.status)}
                      </span>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-bold ${priorityBadgeClass[task.priority]}`}>
                        {labelForPriority(task.priority)}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-slate-600">{task.projectName}</td>
                    <td className="px-6 py-4 text-slate-600">{task.assigneeName ?? '—'}</td>
                    <td className="px-6 py-4 text-slate-600">{formatDate(task.dueDate)}</td>
                    {canManageWorkspace ? (
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          {task.status !== 'Done' ? (
                            <Button
                              variant="ghost"
                              onClick={() => statusMutation.mutate({ taskId: task.id, nextStatus: 'Done' })}
                              disabled={statusMutation.isPending}
                            >
                              Concluir
                            </Button>
                          ) : null}
                          <Button variant="ghost" onClick={() => openEdit(task)}>Editar</Button>
                          <Button variant="ghost" onClick={() => deleteMutation.mutate(task.id)} disabled={deleteMutation.isPending}>
                            <Trash2 className="h-4 w-4 text-red-400" />
                          </Button>
                        </div>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="border-t border-slate-100 bg-slate-50/50 px-6 py-3">
            <span className="text-xs font-medium text-slate-500">{rows.length} {rows.length === 1 ? 'tarefa' : 'tarefas'} encontrada{rows.length === 1 ? '' : 's'}</span>
          </div>
        </div>
      ) : null}

      <Modal
        open={modalOpen}
        title={selectedTask ? 'Editar tarefa' : 'Nova tarefa'}
        description="Alterações são auditadas, respeitam cotas e invalidam caches."
        onClose={() => setModalOpen(false)}
      >
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(draft) }}
        >
          <Field label="Título da tarefa">
            <Input value={draft.title} onChange={(e) => setDraft((c) => ({ ...c, title: e.target.value }))} />
          </Field>
          <Field label="Projeto">
            <Select value={draft.projectId} onChange={(e) => setDraft((c) => ({ ...c, projectId: e.target.value }))}>
              <option value="">Selecionar projeto</option>
              {projectOptions.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </Select>
          </Field>
          <Field label="Status">
            <Select value={draft.status} onChange={(e) => setDraft((c) => ({ ...c, status: e.target.value as WorkTaskStatus }))}>
              {taskStatusOptions.map((opt) => <option key={opt} value={opt}>{labelForTaskStatus(opt)}</option>)}
            </Select>
          </Field>
          <Field label="Prioridade">
            <Select value={draft.priority} onChange={(e) => setDraft((c) => ({ ...c, priority: e.target.value as TaskPriority }))}>
              {taskPriorityOptions.map((opt) => <option key={opt} value={opt}>{labelForPriority(opt)}</option>)}
            </Select>
          </Field>
          <Field label="Responsável">
            <Select value={draft.assigneeUserId ?? ''} onChange={(e) => setDraft((c) => ({ ...c, assigneeUserId: e.target.value || null }))}>
              <option value="">Sem responsável</option>
              {userOptions.map((u) => <option key={u.id} value={u.id}>{u.fullName}</option>)}
            </Select>
          </Field>
          <Field label="Prazo">
            <Input type="date" value={draft.dueDate ?? ''} onChange={(e) => setDraft((c) => ({ ...c, dueDate: e.target.value || null }))} />
          </Field>
          <div className="md:col-span-2">
            <Field label="Descrição">
              <Textarea rows={3} value={draft.description} onChange={(e) => setDraft((c) => ({ ...c, description: e.target.value }))} />
            </Field>
          </div>
          {saveMutation.isError ? (
            <div className="md:col-span-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {problemMessage(saveMutation.error)}
            </div>
          ) : null}
          <div className="flex justify-end gap-3 md:col-span-2">
            <Button type="button" variant="secondary" onClick={() => setModalOpen(false)}>Cancelar</Button>
            <Button disabled={saveMutation.isPending} type="submit">
              {saveMutation.isPending ? 'Salvando...' : selectedTask ? 'Atualizar' : 'Criar tarefa'}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
