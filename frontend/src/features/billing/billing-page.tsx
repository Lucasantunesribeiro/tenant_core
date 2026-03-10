import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { queryKeys, tenantApi } from '../../lib/api'
import { formatDateTime, formatRelativeUsage } from '../../lib/format'
import { labelForQuotaState, planOptions } from '../../lib/lookups'
import type { PlanCode } from '../../types/api'
import { useSession } from '../auth/use-session'
import { problemMessage } from '../../lib/http'
import { AccessNotice, Badge, Button, ErrorState, LoadingState } from '../../components/ui/primitives'

const planNarratives: Record<PlanCode, string> = {
  Free: 'Plano inicial com limites rígidos que incentivam a disciplina operacional.',
  Pro: 'Plano de crescimento para equipes que precisam de mais espaço.',
  Business: 'Nível empresarial para tenants de alto volume com governança avançada.',
}

const planLabels: Record<PlanCode, string> = {
  Free: 'Grátis',
  Pro: 'Pro',
  Business: 'Business',
}

export function BillingPage() {
  const queryClient = useQueryClient()
  const { isAdmin } = useSession()
  const usageQuery = useQuery({ queryKey: queryKeys.usage, queryFn: tenantApi.getUsage })
  const subscriptionQuery = useQuery({ queryKey: queryKeys.subscription, queryFn: tenantApi.getSubscription })

  const changePlanMutation = useMutation({
    mutationFn: tenantApi.changePlan,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['usage'] })
      await queryClient.invalidateQueries({ queryKey: ['subscription'] })
    },
  })

  if (usageQuery.isLoading || subscriptionQuery.isLoading) {
    return <div className="p-8"><LoadingState label="Carregando dados de assinatura..." /></div>
  }

  if (usageQuery.isError || subscriptionQuery.isError) {
    return (
      <div className="p-8">
        <ErrorState
          detail="As informações de assinatura não puderam ser carregadas."
          retry={() => { usageQuery.refetch(); subscriptionQuery.refetch() }}
        />
      </div>
    )
  }

  if (!usageQuery.data || !subscriptionQuery.data) {
    return <div className="p-8"><ErrorState detail="Dados de assinatura incompletos." /></div>
  }

  const usage = usageQuery.data
  const subscription = subscriptionQuery.data
  const quotaTone = usage.plan.quotaState === 'Healthy' ? 'success' : usage.plan.quotaState === 'NearLimit' ? 'warning' : 'danger'

  const usageItems = [
    { label: 'Usuários', value: formatRelativeUsage(usage.usage.activeUsers, subscription.maxUsers), current: usage.usage.activeUsers, limit: subscription.maxUsers },
    { label: 'Projetos', value: formatRelativeUsage(usage.usage.projects, subscription.maxProjects), current: usage.usage.projects, limit: subscription.maxProjects },
    { label: 'Clientes', value: formatRelativeUsage(usage.usage.clients, subscription.maxClients), current: usage.usage.clients, limit: subscription.maxClients },
  ]

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col gap-1 md:flex-row md:items-end md:justify-between">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-primary">Controles Comerciais</p>
          <h1 className="text-3xl font-black tracking-tight text-slate-900">Assinatura e Uso</h1>
          <p className="mt-1 text-sm text-slate-500">
            A aplicação de planos é real. Limites de usuários e projetos são verificados antes de cada operação de escrita.
          </p>
        </div>
        <Badge tone={quotaTone}>{labelForQuotaState(usage.plan.quotaState)}</Badge>
      </div>

      {!isAdmin ? (
        <AccessNotice
          title="Visualização somente leitura"
          detail="Apenas administradores podem alterar planos. Gerentes e usuários podem inspecionar limites e cotas."
        />
      ) : null}

      <div className="grid gap-6 xl:grid-cols-2">
        {/* Current plan */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 space-y-5">
          <div className="flex items-start justify-between">
            <div>
              <p className="text-xs font-bold uppercase tracking-wider text-primary">Plano Atual</p>
              <h2 className="mt-1 text-2xl font-black text-slate-900">{subscription.planName}</h2>
            </div>
            <Badge tone={quotaTone}>{labelForQuotaState(subscription.quotaState)}</Badge>
          </div>

          <p className="text-sm text-slate-500">{subscription.description}</p>

          <div className="space-y-4">
            <h3 className="text-sm font-bold text-slate-700">Limites e Uso</h3>
            {usageItems.map((item) => {
              const ratio = item.limit > 0 ? Math.min(100, Math.round((item.current / item.limit) * 100)) : 0
              const barColor = ratio >= 90 ? 'bg-red-500' : ratio >= 70 ? 'bg-amber-500' : 'bg-primary'
              return (
                <div key={item.label} className="space-y-1.5">
                  <div className="flex justify-between text-sm">
                    <span className="font-medium text-slate-700">{item.label}</span>
                    <span className="text-slate-500">{item.value}</span>
                  </div>
                  <div className="h-2 w-full overflow-hidden rounded-full bg-slate-100">
                    <div className={`h-full rounded-full transition-all ${barColor}`} style={{ width: `${ratio}%` }} />
                  </div>
                </div>
              )
            })}
          </div>

          <dl className="space-y-3 border-t border-slate-100 pt-4">
            {[
              ['Renovado em', formatDateTime(subscription.renewedAtUtc)],
              ['Próxima renovação', formatDateTime(subscription.nextRenewalAtUtc)],
            ].map(([label, value]) => (
              <div key={label} className="flex items-center justify-between gap-4">
                <dt className="text-sm text-slate-500">{label}</dt>
                <dd className="text-sm font-semibold text-slate-900">{value}</dd>
              </div>
            ))}
          </dl>

          {subscription.warningMessage ? (
            <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-700">
              {subscription.warningMessage}
            </div>
          ) : null}
        </div>

        {/* Plan simulator */}
        <div className="rounded-xl border border-slate-200 bg-white p-6 space-y-5">
          <div>
            <p className="text-xs font-bold uppercase tracking-wider text-primary">Simulador de Planos</p>
            <h2 className="mt-1 text-xl font-black text-slate-900">Trocar de plano SaaS</h2>
            <p className="mt-1 text-sm text-slate-500">
              A API atualiza a assinatura e a página recarrega o uso para refletir as mudanças de cota imediatamente.
            </p>
          </div>

          <div className="space-y-3">
            {planOptions.map((plan) => {
              const isCurrent = plan === subscription.planCode
              return (
                <div key={plan} className={`rounded-xl border p-5 transition-colors ${isCurrent ? 'border-primary bg-primary/5' : 'border-slate-200 bg-white'}`}>
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <h3 className="font-bold text-slate-900">{planLabels[plan]}</h3>
                      <p className="mt-1 text-sm text-slate-500">{planNarratives[plan]}</p>
                    </div>
                    {isCurrent ? <Badge tone="success">Plano atual</Badge> : null}
                  </div>
                  <div className="mt-4 flex justify-end">
                    <Button
                      disabled={!isAdmin || isCurrent || changePlanMutation.isPending}
                      onClick={() => changePlanMutation.mutate(plan)}
                      variant={isCurrent ? 'secondary' : 'primary'}
                    >
                      {isCurrent ? 'Ativo' : changePlanMutation.isPending ? 'Aplicando...' : `Mudar para ${planLabels[plan]}`}
                    </Button>
                  </div>
                </div>
              )
            })}
          </div>

          {changePlanMutation.isError ? (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {problemMessage(changePlanMutation.error)}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  )
}
