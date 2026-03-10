import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { clientsApi, queryKeys } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { clientStatusOptions } from '../../lib/lookups'
import type { ClientListItem, ClientStatus } from '../../types/api'
import { useSession } from '../auth/use-session'
import { problemMessage } from '../../lib/http'
import {
  Button,
  EmptyState,
  ErrorState,
  Field,
  Input,
  LoadingState,
  Modal,
  Select,
  Textarea,
} from '../../components/ui/primitives'

type ClientDraft = {
  name: string
  email: string
  contactName: string
  status: ClientStatus
  notes: string
}

const emptyDraft: ClientDraft = { name: '', email: '', contactName: '', status: 'Lead', notes: '' }

const statusLabels: Record<ClientStatus, string> = {
  Lead: 'Lead',
  Active: 'Ativo',
  Inactive: 'Inativo',
}

const statusBadgeClass: Record<ClientStatus, string> = {
  Lead: 'bg-blue-100 text-blue-700 border-blue-200',
  Active: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  Inactive: 'bg-slate-100 text-slate-600 border-slate-200',
}

const statusDotClass: Record<ClientStatus, string> = {
  Lead: 'bg-blue-500',
  Active: 'bg-emerald-500',
  Inactive: 'bg-slate-400',
}

export function ClientsPage() {
  const queryClient = useQueryClient()
  const { canManageWorkspace } = useSession()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [selectedClient, setSelectedClient] = useState<ClientListItem | null>(null)
  const [draft, setDraft] = useState<ClientDraft>(emptyDraft)

  const clientsQuery = useQuery({
    queryKey: queryKeys.clients(search, status),
    queryFn: () => clientsApi.list(search, status),
  })

  const saveMutation = useMutation({
    mutationFn: async (payload: ClientDraft) => {
      if (selectedClient) {
        await clientsApi.update(selectedClient.id, payload)
        return
      }
      await clientsApi.create(payload)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['clients'] })
      setModalOpen(false)
      setSelectedClient(null)
      setDraft(emptyDraft)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: clientsApi.remove,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['clients'] })
    },
  })

  const rows = useMemo(() => clientsQuery.data?.items ?? [], [clientsQuery.data])

  function openCreate() {
    setSelectedClient(null)
    setDraft(emptyDraft)
    setModalOpen(true)
  }

  function openEdit(client: ClientListItem) {
    setSelectedClient(client)
    setDraft({ name: client.name, email: client.email, contactName: client.contactName, status: client.status, notes: client.notes })
    setModalOpen(true)
  }

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Portfólio de Clientes</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Gestão de Clientes</h1>
          <p className="mt-1 text-sm text-slate-500">Visualize e gerencie todos os clientes e parceiros corporativos.</p>
        </div>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => { setSearch(''); setStatus('') }}>
            <span className="material-symbols-outlined text-[18px] mr-1">filter_list_off</span>
            Limpar
          </Button>
          {canManageWorkspace ? (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" />
              Novo Cliente
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
            placeholder="Pesquisar clientes..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <select
          value={status}
          onChange={(e) => setStatus(e.target.value)}
          className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20"
        >
          <option value="">Todos os status</option>
          {clientStatusOptions.map((opt) => (
            <option key={opt} value={opt}>{statusLabels[opt]}</option>
          ))}
        </select>
      </div>

      {/* States */}
      {clientsQuery.isLoading ? <LoadingState label="Carregando clientes..." /> : null}
      {clientsQuery.isError ? <ErrorState detail="Os dados de clientes não puderam ser carregados." retry={() => clientsQuery.refetch()} /> : null}
      {!clientsQuery.isLoading && !clientsQuery.isError && rows.length === 0 ? (
        <EmptyState
          title="Nenhum cliente encontrado"
          description="Crie o primeiro cliente ou ajuste os filtros para visualizar os existentes."
          action={canManageWorkspace ? <Button onClick={openCreate}>Criar cliente</Button> : undefined}
        />
      ) : null}

      {/* Table */}
      {!clientsQuery.isLoading && !clientsQuery.isError && rows.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50">
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Empresa</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Contato</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Status</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Observações</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Atualizado</th>
                  {canManageWorkspace ? <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500 text-right">Ações</th> : null}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((client) => (
                  <tr key={client.id} className="transition-colors hover:bg-slate-50/60">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-sm font-bold text-primary">
                          {client.name.slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <p className="font-bold text-slate-900">{client.name}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <p className="font-medium text-slate-700">{client.contactName}</p>
                      <p className="text-xs text-slate-500">{client.email}</p>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-bold ${statusBadgeClass[client.status]}`}>
                        <span className={`h-1.5 w-1.5 rounded-full ${statusDotClass[client.status]}`} />
                        {statusLabels[client.status]}
                      </span>
                    </td>
                    <td className="px-6 py-4 max-w-xs text-sm text-slate-500">{client.notes || '—'}</td>
                    <td className="px-6 py-4 text-sm text-slate-500">{formatDateTime(client.updatedAtUtc)}</td>
                    {canManageWorkspace ? (
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <Button variant="ghost" onClick={() => openEdit(client)}>
                            Editar
                          </Button>
                          <Button
                            variant="ghost"
                            onClick={() => deleteMutation.mutate(client.id)}
                            disabled={deleteMutation.isPending}
                          >
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

          <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50/50 px-6 py-3">
            <span className="text-xs font-medium text-slate-500">
              {rows.length} {rows.length === 1 ? 'cliente' : 'clientes'} encontrado{rows.length === 1 ? '' : 's'}
            </span>
          </div>
        </div>
      ) : null}

      <Modal
        open={modalOpen}
        title={selectedClient ? 'Editar cliente' : 'Novo cliente'}
        description="As alterações são auditadas e restritas ao tenant atual."
        onClose={() => setModalOpen(false)}
      >
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={(event) => {
            event.preventDefault()
            saveMutation.mutate(draft)
          }}
        >
          <Field label="Nome do cliente">
            <Input value={draft.name} onChange={(e) => setDraft((c) => ({ ...c, name: e.target.value }))} />
          </Field>
          <Field label="Nome do contato">
            <Input value={draft.contactName} onChange={(e) => setDraft((c) => ({ ...c, contactName: e.target.value }))} />
          </Field>
          <Field label="E-mail de contato">
            <Input type="email" value={draft.email} onChange={(e) => setDraft((c) => ({ ...c, email: e.target.value }))} />
          </Field>
          <Field label="Status">
            <Select value={draft.status} onChange={(e) => setDraft((c) => ({ ...c, status: e.target.value as ClientStatus }))}>
              {clientStatusOptions.map((opt) => (
                <option key={opt} value={opt}>{statusLabels[opt]}</option>
              ))}
            </Select>
          </Field>
          <div className="md:col-span-2">
            <Field label="Observações">
              <Textarea rows={3} value={draft.notes} onChange={(e) => setDraft((c) => ({ ...c, notes: e.target.value }))} />
            </Field>
          </div>
          {saveMutation.isError ? (
            <div className="md:col-span-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {problemMessage(saveMutation.error)}
            </div>
          ) : null}
          <div className="flex justify-end gap-3 md:col-span-2">
            <Button type="button" variant="secondary" onClick={() => setModalOpen(false)}>
              Cancelar
            </Button>
            <Button disabled={saveMutation.isPending} type="submit">
              {saveMutation.isPending ? 'Salvando...' : selectedClient ? 'Atualizar' : 'Criar cliente'}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
