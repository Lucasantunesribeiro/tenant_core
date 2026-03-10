import { useQuery } from '@tanstack/react-query'
import { auditApi, queryKeys } from '../../lib/api'
import { formatDateTime } from '../../lib/format'
import { useSession } from '../auth/use-session'
import { AccessNotice, EmptyState, ErrorState, LoadingState } from '../../components/ui/primitives'
import { useState } from 'react'

export function AuditPage() {
  const { isAdmin } = useSession()
  const [action, setAction] = useState('')
  const [entityType, setEntityType] = useState('')

  const auditQuery = useQuery({
    queryKey: queryKeys.auditLogs(action, entityType),
    queryFn: () => auditApi.list(action, entityType),
    enabled: isAdmin,
  })

  if (!isAdmin) {
    return (
      <div className="p-6 md:p-8 space-y-6">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Governança</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Logs de Auditoria</h1>
          <p className="mt-1 text-sm text-slate-500">
            Este feed contém eventos sensíveis do tenant e é restrito a administradores.
          </p>
        </div>
        <AccessNotice title="Área exclusiva para admins" detail="Use esta área para demonstrar rastreabilidade de autenticação, cobrança e mutações do workspace." />
      </div>
    )
  }

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div>
        <p className="text-xs font-bold uppercase tracking-wider text-primary">Governança</p>
        <h1 className="text-3xl font-black tracking-tight text-slate-900">Logs de Auditoria</h1>
        <p className="mt-1 text-sm text-slate-500">
          Histórico de eventos estruturado para ações sensíveis, com escopo por tenant e correlacionado com metadados de requisição.
        </p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-3">
        <div className="relative max-w-sm flex-1">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-xl">search</span>
          <input
            type="text"
            placeholder="Filtrar por ação (ex: auth.login)..."
            value={action}
            onChange={(e) => setAction(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
        <div className="relative max-w-sm flex-1">
          <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-xl">category</span>
          <input
            type="text"
            placeholder="Filtrar por entidade (ex: Project)..."
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
            className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
          />
        </div>
      </div>

      {auditQuery.isLoading ? <LoadingState label="Carregando log de auditoria..." /> : null}
      {auditQuery.isError ? <ErrorState detail="Os dados de auditoria não puderam ser carregados." retry={() => auditQuery.refetch()} /> : null}
      {!auditQuery.isLoading && !auditQuery.isError && (auditQuery.data?.items.length ?? 0) === 0 ? (
        <EmptyState
          title="Nenhum evento de auditoria encontrado"
          description="Ajuste os filtros ou realize uma ação sensível como login, mudança de função ou troca de plano."
        />
      ) : null}

      {!auditQuery.isLoading && !auditQuery.isError && (auditQuery.data?.items.length ?? 0) > 0 ? (
        <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
          <div className="flex items-center justify-between border-b border-slate-100 bg-slate-50 px-6 py-4">
            <h2 className="text-base font-bold text-slate-900">Registro de Auditoria</h2>
            <span className="text-xs font-medium text-slate-500">{auditQuery.data?.items.length} eventos</span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="border-b border-slate-100 bg-slate-50/50">
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Ação</th>
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Entidade</th>
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Autor</th>
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Correlação</th>
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Metadados</th>
                  <th className="px-6 py-3 text-xs font-bold uppercase tracking-wider text-slate-500">Quando</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {auditQuery.data?.items.map((entry) => (
                  <tr key={entry.id} className="transition-colors hover:bg-slate-50/60">
                    <td className="px-6 py-4 font-semibold text-slate-900">{entry.action}</td>
                    <td className="px-6 py-4">
                      <p className="font-medium text-slate-800">{entry.entityType}</p>
                      <p className="text-xs text-slate-400 font-mono">{entry.entityId}</p>
                    </td>
                    <td className="px-6 py-4">
                      <div className="flex items-center gap-2">
                        <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-slate-200 text-[10px] font-bold text-slate-600" />
                        <span className="text-sm text-slate-700">{entry.actorUserId ?? 'Sistema'}</span>
                      </div>
                    </td>
                    <td className="px-6 py-4 font-mono text-xs text-slate-400">{entry.correlationId}</td>
                    <td className="px-6 py-4 max-w-xs font-mono text-xs text-slate-400">{entry.metadataJson}</td>
                    <td className="px-6 py-4 text-sm text-slate-500">{formatDateTime(entry.occurredAtUtc)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : null}
    </div>
  )
}
