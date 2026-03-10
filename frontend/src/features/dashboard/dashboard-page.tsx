import { useQuery } from '@tanstack/react-query'
import { queryKeys, tenantApi } from '../../lib/api'
import { formatDateTime, formatRelativeUsage } from '../../lib/format'
import { labelForQuotaState } from '../../lib/lookups'
import { useSession } from '../auth/use-session'
import { Badge, ErrorState, LoadingState } from '../../components/ui/primitives'

export function DashboardPage() {
  const { session } = useSession()
  const profileQuery = useQuery({ queryKey: queryKeys.tenantProfile, queryFn: tenantApi.getProfile })
  const usageQuery = useQuery({ queryKey: queryKeys.usage, queryFn: tenantApi.getUsage })
  const subscriptionQuery = useQuery({ queryKey: queryKeys.subscription, queryFn: tenantApi.getSubscription })

  if (profileQuery.isLoading || usageQuery.isLoading || subscriptionQuery.isLoading) {
    return (
      <div className="p-8">
        <LoadingState label="Carregando painel do tenant..." />
      </div>
    )
  }

  if (profileQuery.isError || usageQuery.isError || subscriptionQuery.isError) {
    return (
      <div className="p-8">
        <ErrorState
          detail="O painel não pôde carregar as métricas. Verifique a API, o cabeçalho do tenant e os dados iniciais."
          retry={() => { profileQuery.refetch(); usageQuery.refetch(); subscriptionQuery.refetch() }}
        />
      </div>
    )
  }

  if (!profileQuery.data || !usageQuery.data || !subscriptionQuery.data) {
    return <div className="p-8"><ErrorState detail="Dados do painel incompletos." /></div>
  }

  const profile = profileQuery.data
  const usage = usageQuery.data
  const subscription = subscriptionQuery.data

  const quotaTone = usage.plan.quotaState === 'Healthy' ? 'success' : usage.plan.quotaState === 'NearLimit' ? 'warning' : 'danger'

  const metricCards = [
    { label: 'Usuários Ativos', value: usage.usage.activeUsers, detail: formatRelativeUsage(usage.usage.activeUsers, usage.plan.maxUsers), icon: 'group' },
    { label: 'Projetos', value: usage.usage.projects, detail: formatRelativeUsage(usage.usage.projects, usage.plan.maxProjects), icon: 'folder_open' },
    { label: 'Tarefas', value: usage.usage.tasks, detail: 'Entregas operacionais', icon: 'task_alt' },
    { label: 'Clientes', value: usage.usage.clients, detail: formatRelativeUsage(usage.usage.clients, usage.plan.maxClients), icon: 'groups' },
  ]

  const quotaBars = [
    { label: 'Usuários', current: usage.usage.activeUsers, limit: usage.plan.maxUsers },
    { label: 'Projetos', current: usage.usage.projects, limit: usage.plan.maxProjects },
    { label: 'Clientes', current: usage.usage.clients, limit: usage.plan.maxClients },
  ]

  return (
    <div className="p-6 md:p-8 space-y-8">
      {/* Header */}
      <div className="flex flex-col gap-1 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Visão Geral</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">
            Bem-vindo, {session?.user.fullName.split(' ')[0] ?? 'operador'}
          </h1>
          <p className="mt-1 text-sm text-slate-500">
            {profile.name} está no plano {subscription.planName} com limites de workspace ativos.
          </p>
        </div>
        <Badge tone={quotaTone}>{labelForQuotaState(usage.plan.quotaState)}</Badge>
      </div>

      {/* Metric cards */}
      <div className="grid gap-5 sm:grid-cols-2 xl:grid-cols-4">
        {metricCards.map((item) => (
          <div key={item.label} className="relative overflow-hidden rounded-xl border border-slate-200 bg-white p-6">
            <div className="absolute right-0 top-0 -mr-8 -mt-8 h-24 w-24 rounded-full bg-primary/5" />
            <div className="mb-3 flex items-center justify-between">
              <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">{item.label}</p>
              <span className="material-symbols-outlined text-slate-400 text-xl">{item.icon}</span>
            </div>
            <p className="text-4xl font-black text-slate-900">{item.value}</p>
            <p className="mt-1 text-xs text-slate-500">{item.detail}</p>
          </div>
        ))}
      </div>

      {/* Body panels */}
      <div className="grid gap-6 xl:grid-cols-[1.4fr_1fr]">
        {/* Quota health */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 space-y-5">
          <div className="flex items-start justify-between gap-4">
            <div>
              <p className="text-xs font-bold uppercase tracking-wider text-primary">Saúde do Workspace</p>
              <h2 className="mt-1 text-xl font-black text-slate-900">Cotas e Uso</h2>
            </div>
            <Badge tone={quotaTone}>{labelForQuotaState(usage.plan.quotaState)}</Badge>
          </div>

          <div className="space-y-4">
            {quotaBars.map((item) => {
              const ratio = item.limit > 0 ? Math.min(100, Math.round((item.current / item.limit) * 100)) : 0
              const barColor = ratio >= 90 ? 'bg-red-500' : ratio >= 70 ? 'bg-amber-500' : 'bg-primary'
              return (
                <div key={item.label} className="space-y-1.5">
                  <div className="flex justify-between text-sm">
                    <span className="font-medium text-slate-700">{item.label}</span>
                    <span className="text-slate-500">{item.current} / {item.limit}</span>
                  </div>
                  <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
                    <div className={`h-full rounded-full transition-all ${barColor}`} style={{ width: `${ratio}%` }} />
                  </div>
                </div>
              )
            })}
          </div>

          <div className="rounded-xl border border-slate-100 bg-slate-50 p-4">
            <p className="text-sm font-semibold text-slate-700">Último snapshot de uso</p>
            <p className="mt-1 text-sm text-slate-500">{formatDateTime(usage.usage.lastSnapshotAtUtc)}</p>
            {subscription.warningMessage ? (
              <p className="mt-2 text-sm text-amber-600">{subscription.warningMessage}</p>
            ) : null}
          </div>
        </div>

        {/* Tenant profile */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 space-y-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-primary">Perfil do Tenant</p>
            <h2 className="mt-1 text-xl font-black text-slate-900">Informações Operacionais</h2>
          </div>

          <dl className="space-y-3">
            {[
              ['Plano', subscription.planName],
              ['Renovação', formatDateTime(subscription.nextRenewalAtUtc)],
              ['E-mail de cobrança', profile.billingEmail],
              ['E-mail de suporte', profile.supportEmail],
              ['Fuso horário', profile.timeZone],
              ['Domínios permitidos', profile.allowedDomains || 'Sem restrição'],
            ].map(([label, value]) => (
              <div key={label} className="flex items-start justify-between gap-4 border-b border-slate-100 pb-3 last:border-0 last:pb-0">
                <dt className="text-sm text-slate-500 shrink-0">{label}</dt>
                <dd className="text-sm font-semibold text-slate-900 text-right max-w-[60%]">{value}</dd>
              </div>
            ))}
          </dl>
        </div>
      </div>
    </div>
  )
}
