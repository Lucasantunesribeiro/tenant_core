import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { clientsApi, projectsApi, queryKeys, usersApi } from '../../lib/api'
import { formatDate, formatDateTime } from '../../lib/format'
import { projectStatusOptions } from '../../lib/lookups'
import { problemMessage } from '../../lib/http'
import type { ProjectListItem, ProjectStatus } from '../../types/api'
import { useSession } from '../auth/use-session'
import { Button, EmptyState, ErrorState, Field, Input, LoadingState, Modal, Select, Textarea } from '../../components/ui/primitives'

type ProjectDraft = {
  clientId: string | null
  ownerUserId: string | null
  name: string
  code: string
  description: string
  status: ProjectStatus
  startDate: string | null
  dueDate: string | null
}

const emptyDraft: ProjectDraft = {
  clientId: null, ownerUserId: null, name: '', code: '', description: '', status: 'Planned', startDate: null, dueDate: null,
}

const statusLabels: Record<ProjectStatus, string> = {
  Planned: 'Planejado',
  Active: 'Ativo',
  Completed: 'Concluído',
  Archived: 'Arquivado',
}

const statusBadgeClass: Record<ProjectStatus, string> = {
  Planned: 'bg-blue-100 text-blue-700 border-blue-200',
  Active: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  Completed: 'bg-slate-100 text-slate-600 border-slate-200',
  Archived: 'bg-slate-100 text-slate-500 border-slate-200',
}

export function ProjectsPage() {
  const queryClient = useQueryClient()
  const { canManageWorkspace } = useSession()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [selectedProject, setSelectedProject] = useState<ProjectListItem | null>(null)
  const [draft, setDraft] = useState<ProjectDraft>(emptyDraft)

  const projectsQuery = useQuery({ queryKey: queryKeys.projects(search, status), queryFn: () => projectsApi.list(search, status) })
  const clientsQuery = useQuery({ queryKey: queryKeys.clients(), queryFn: () => clientsApi.list() })
  const usersQuery = useQuery({ queryKey: queryKeys.users(), queryFn: () => usersApi.list(), enabled: canManageWorkspace })

  const saveMutation = useMutation({
    mutationFn: async (payload: ProjectDraft) => {
      if (selectedProject) { await projectsApi.update(selectedProject.id, payload); return }
      await projectsApi.create(payload)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['projects'] })
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
      setModalOpen(false); setSelectedProject(null); setDraft(emptyDraft)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: projectsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['projects'] })
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
    },
  })

  const rows = useMemo(() => projectsQuery.data?.items ?? [], [projectsQuery.data])
  const clientOptions = clientsQuery.data?.items ?? []
  const userOptions = usersQuery.data?.items ?? []

  function openCreate() { setSelectedProject(null); setDraft(emptyDraft); setModalOpen(true) }

  function openEdit(project: ProjectListItem) {
    setSelectedProject(project)
    setDraft({ clientId: project.clientId, ownerUserId: project.ownerUserId, name: project.name, code: project.code, description: '', status: project.status, startDate: null, dueDate: project.dueDate })
    setModalOpen(true)
  }

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Portfólio de Entrega</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Projetos</h1>
          <p className="mt-1 text-sm text-slate-500">Monitore execução, responsáveis e alinhamento com clientes.</p>
        </div>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => { setSearch(''); setStatus('') }}>
            <span className="material-symbols-outlined text-[18px] mr-1">filter_list_off</span>
            Limpar
          </Button>
          {canManageWorkspace ? (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" />
              Novo Projeto
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
            placeholder="Pesquisar projetos..."
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
          {projectStatusOptions.map((opt) => (
            <option key={opt} value={opt}>{statusLabels[opt]}</option>
          ))}
        </select>
      </div>

      {projectsQuery.isLoading ? <LoadingState label="Carregando projetos..." /> : null}
      {projectsQuery.isError ? <ErrorState detail="Os dados de projetos não puderam ser carregados." retry={() => projectsQuery.refetch()} /> : null}
      {!projectsQuery.isLoading && !projectsQuery.isError && rows.length === 0 ? (
        <EmptyState
          title="Nenhum projeto encontrado"
          description="Crie um projeto para demonstrar aplicação de planos, responsabilidade e log de auditoria."
          action={canManageWorkspace ? <Button onClick={openCreate}>Criar projeto</Button> : undefined}
        />
      ) : null}

      {!projectsQuery.isLoading && !projectsQuery.isError && rows.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50">
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Projeto</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Status</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Cliente</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Responsável</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Tarefas</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Prazo</th>
                  {canManageWorkspace ? <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500 text-right">Ações</th> : null}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((project) => (
                  <tr key={project.id} className="transition-colors hover:bg-slate-50/60">
                    <td className="px-6 py-4">
                      <p className="font-bold text-slate-900">{project.name}</p>
                      <p className="text-xs text-slate-500 mt-0.5">{project.code} · {formatDateTime(project.updatedAtUtc)}</p>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${statusBadgeClass[project.status]}`}>
                        {statusLabels[project.status]}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-slate-600">{project.clientName ?? '—'}</td>
                    <td className="px-6 py-4 text-slate-600">{project.ownerName ?? '—'}</td>
                    <td className="px-6 py-4 text-slate-700 font-semibold">{project.taskCount}</td>
                    <td className="px-6 py-4 text-slate-600">{formatDate(project.dueDate)}</td>
                    {canManageWorkspace ? (
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button variant="ghost" onClick={() => openEdit(project)}>Editar</Button>
                          <Button variant="ghost" onClick={() => deleteMutation.mutate(project.id)} disabled={deleteMutation.isPending}>
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
            <span className="text-xs font-medium text-slate-500">{rows.length} {rows.length === 1 ? 'projeto' : 'projetos'} encontrado{rows.length === 1 ? '' : 's'}</span>
          </div>
        </div>
      ) : null}

      <Modal
        open={modalOpen}
        title={selectedProject ? 'Editar projeto' : 'Novo projeto'}
        description="Alterações de projeto invalidam o cache de uso e geram entradas de auditoria."
        onClose={() => setModalOpen(false)}
      >
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={(event) => { event.preventDefault(); saveMutation.mutate(draft) }}
        >
          <Field label="Nome do projeto">
            <Input value={draft.name} onChange={(e) => setDraft((c) => ({ ...c, name: e.target.value }))} />
          </Field>
          <Field label="Código">
            <Input value={draft.code} onChange={(e) => setDraft((c) => ({ ...c, code: e.target.value.toUpperCase() }))} />
          </Field>
          <Field label="Cliente">
            <Select value={draft.clientId ?? ''} onChange={(e) => setDraft((c) => ({ ...c, clientId: e.target.value || null }))}>
              <option value="">Nenhum cliente</option>
              {clientOptions.map((client) => <option key={client.id} value={client.id}>{client.name}</option>)}
            </Select>
          </Field>
          <Field label="Responsável">
            <Select value={draft.ownerUserId ?? ''} onChange={(e) => setDraft((c) => ({ ...c, ownerUserId: e.target.value || null }))}>
              <option value="">Sem responsável</option>
              {userOptions.map((user) => <option key={user.id} value={user.id}>{user.fullName}</option>)}
            </Select>
          </Field>
          <Field label="Status">
            <Select value={draft.status} onChange={(e) => setDraft((c) => ({ ...c, status: e.target.value as ProjectStatus }))}>
              {projectStatusOptions.map((opt) => <option key={opt} value={opt}>{statusLabels[opt]}</option>)}
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
              {saveMutation.isPending ? 'Salvando...' : selectedProject ? 'Atualizar' : 'Criar projeto'}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
