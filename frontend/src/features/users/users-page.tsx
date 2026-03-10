import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useMemo, useState } from 'react'
import { queryKeys, usersApi } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { problemMessage } from '../../lib/http'
import { roleOptions } from '../../lib/lookups'
import type { UserRole } from '../../types/api'
import { useSession } from '../auth/use-session'
import { AccessNotice, Button, EmptyState, ErrorState, Field, Input, LoadingState, Modal, Select } from '../../components/ui/primitives'

type UserDraft = { email: string; fullName: string; password: string; role: UserRole; invitationPending: boolean }
const emptyDraft: UserDraft = { email: '', fullName: '', password: 'Passw0rd!', role: 'User', invitationPending: false }

const roleLabels: Record<UserRole, string> = { Admin: 'Administrador', Manager: 'Gerente', User: 'Usuário' }

const roleBadgeClass: Record<UserRole, string> = {
  Admin: 'bg-amber-100 text-amber-700 border-amber-200',
  Manager: 'bg-blue-100 text-blue-700 border-blue-200',
  User: 'bg-slate-100 text-slate-600 border-slate-200',
}

export function UsersPage() {
  const queryClient = useQueryClient()
  const { session, isAdmin, isManager } = useSession()
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [draft, setDraft] = useState<UserDraft>(emptyDraft)

  const usersQuery = useQuery({
    queryKey: queryKeys.users(search, role),
    queryFn: () => usersApi.list(search, role),
    enabled: isAdmin || isManager,
  })

  const createMutation = useMutation({
    mutationFn: usersApi.create,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['users'] })
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
      setDraft(emptyDraft); setModalOpen(false)
    },
  })

  const roleMutation = useMutation({
    mutationFn: async ({ userId, nextRole }: { userId: string; nextRole: UserRole }) => usersApi.changeRole(userId, nextRole),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['users'] }) },
  })

  const rows = useMemo(() => usersQuery.data?.items ?? [], [usersQuery.data])

  if (!isAdmin && !isManager) {
    return (
      <div className="p-6 md:p-8 space-y-6">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Diretório de Identidade</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Usuários & Funções</h1>
          <p className="mt-1 text-sm text-slate-500">Área restrita a gerentes e administradores.</p>
        </div>
        <AccessNotice title="Módulo restrito" detail="Sua função permite usar o workspace, mas a visibilidade da equipe é intencionalmente limitada." />
      </div>
    )
  }

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Diretório de Identidade</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Usuários & Funções</h1>
          <p className="mt-1 text-sm text-slate-500">Controle membros, delegação de funções e convites do workspace.</p>
        </div>
        <div className="flex gap-3">
          <Button variant="secondary" onClick={() => { setSearch(''); setRole('') }}>
            <span className="material-symbols-outlined text-[18px] mr-1">filter_list_off</span>
            Limpar
          </Button>
          {isAdmin ? (
            <Button onClick={() => setModalOpen(true)}>
              <Plus className="mr-1.5 h-4 w-4" />
              Convidar usuário
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
            placeholder="Pesquisar por nome ou e-mail..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <select
          value={role}
          onChange={(e) => setRole(e.target.value)}
          className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-700 outline-none focus:border-primary"
        >
          <option value="">Todas as funções</option>
          {roleOptions.map((opt) => <option key={opt} value={opt}>{roleLabels[opt]}</option>)}
        </select>
      </div>

      {usersQuery.isLoading ? <LoadingState label="Carregando diretório de usuários..." /> : null}
      {usersQuery.isError ? <ErrorState detail="O diretório de usuários não pôde ser carregado." retry={() => usersQuery.refetch()} /> : null}
      {!usersQuery.isLoading && !usersQuery.isError && rows.length === 0 ? (
        <EmptyState
          title="Nenhum usuário encontrado"
          description="Ajuste os filtros ou convide o primeiro membro da equipe."
          action={isAdmin ? <Button onClick={() => setModalOpen(true)}>Convidar usuário</Button> : undefined}
        />
      ) : null}

      {!usersQuery.isLoading && !usersQuery.isError && rows.length > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50">
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Usuário</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Função</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Status</th>
                  <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500">Último login</th>
                  {isAdmin ? <th className="px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500 text-right">Alterar função</th> : null}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((user) => (
                  <tr key={user.id} className="transition-colors hover:bg-slate-50/60">
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-3">
                        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary/10 text-xs font-bold text-primary">
                          {user.fullName.split(' ').map((n) => n[0]).join('').slice(0, 2).toUpperCase()}
                        </div>
                        <div>
                          <p className="font-bold text-slate-900">{user.fullName}</p>
                          <p className="text-xs text-slate-500">{user.email}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4">
                      <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${roleBadgeClass[user.role]}`}>
                        {roleLabels[user.role]}
                      </span>
                    </td>
                    <td className="px-6 py-4">
                      {user.invitationPending ? (
                        <span className="inline-flex items-center gap-1.5 rounded-full border border-amber-200 bg-amber-100 px-2.5 py-0.5 text-xs font-bold text-amber-700">
                          Convite pendente
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-200 bg-emerald-100 px-2.5 py-0.5 text-xs font-bold text-emerald-700">
                          <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                          Ativo
                        </span>
                      )}
                    </td>
                    <td className="px-6 py-4 text-slate-500 text-sm">{formatDateTime(user.lastLoginAtUtc)}</td>
                    {isAdmin ? (
                      <td className="px-6 py-4 text-right">
                        <select
                          className="ml-auto rounded-xl border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-slate-700 outline-none focus:border-primary focus:ring-2 focus:ring-primary/20 max-w-40"
                          value={user.role}
                          disabled={roleMutation.isPending || user.id === session?.user.id}
                          onChange={(e) => roleMutation.mutate({ userId: user.id, nextRole: e.target.value as UserRole })}
                        >
                          {roleOptions.map((opt) => <option key={opt} value={opt}>{roleLabels[opt]}</option>)}
                        </select>
                      </td>
                    ) : null}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="border-t border-slate-100 bg-slate-50/50 px-6 py-3">
            <span className="text-xs font-medium text-slate-500">{rows.length} {rows.length === 1 ? 'usuário' : 'usuários'} encontrado{rows.length === 1 ? '' : 's'}</span>
          </div>
        </div>
      ) : null}

      <Modal
        open={modalOpen}
        title="Convidar usuário"
        description="Admins podem criar usuários diretamente. A API persiste a senha com hash e uma entrada de auditoria."
        onClose={() => setModalOpen(false)}
      >
        <form
          className="grid gap-4 md:grid-cols-2"
          onSubmit={(e) => { e.preventDefault(); createMutation.mutate(draft) }}
        >
          <Field label="Nome completo">
            <Input value={draft.fullName} onChange={(e) => setDraft((c) => ({ ...c, fullName: e.target.value }))} />
          </Field>
          <Field label="E-mail">
            <Input type="email" value={draft.email} onChange={(e) => setDraft((c) => ({ ...c, email: e.target.value }))} />
          </Field>
          <Field label="Função">
            <Select value={draft.role} onChange={(e) => setDraft((c) => ({ ...c, role: e.target.value as UserRole }))}>
              {roleOptions.map((opt) => <option key={opt} value={opt}>{roleLabels[opt]}</option>)}
            </Select>
          </Field>
          <Field label="Senha temporária">
            <Input type="password" value={draft.password} onChange={(e) => setDraft((c) => ({ ...c, password: e.target.value }))} />
          </Field>
          <div className="md:col-span-2 rounded-xl border border-slate-200 bg-slate-50 p-4">
            <label className="flex items-center gap-3 text-sm text-slate-700 cursor-pointer">
              <input
                checked={draft.invitationPending}
                onChange={(e) => setDraft((c) => ({ ...c, invitationPending: e.target.checked }))}
                type="checkbox"
                className="h-4 w-4 rounded border-slate-300 text-primary focus:ring-primary"
              />
              Marcar como convite pendente (não totalmente ativo)
            </label>
          </div>
          {createMutation.isError ? (
            <div className="md:col-span-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {problemMessage(createMutation.error)}
            </div>
          ) : null}
          <div className="flex justify-end gap-3 md:col-span-2">
            <Button type="button" variant="secondary" onClick={() => setModalOpen(false)}>Cancelar</Button>
            <Button disabled={createMutation.isPending} type="submit">
              {createMutation.isPending ? 'Criando...' : 'Criar usuário'}
            </Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
