import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { queryKeys, tenantApi } from '../../lib/api'
import { problemMessage } from '../../lib/http'
import { useSession } from '../auth/use-session'
import { AccessNotice, Button, ErrorState, Field, Input, LoadingState, Textarea } from '../../components/ui/primitives'

const settingsSchema = z.object({
  name: z.string().min(3),
  billingEmail: z.string().email(),
  supportEmail: z.string().email(),
  timeZone: z.string().min(2),
  theme: z.string().min(2),
  allowedDomains: z.string(),
})

type SettingsForm = z.infer<typeof settingsSchema>

export function SettingsPage() {
  const queryClient = useQueryClient()
  const { isAdmin } = useSession()
  const profileQuery = useQuery({ queryKey: queryKeys.tenantProfile, queryFn: tenantApi.getProfile })

  const form = useForm<SettingsForm>({
    resolver: zodResolver(settingsSchema),
    defaultValues: { name: '', billingEmail: '', supportEmail: '', timeZone: '', theme: '', allowedDomains: '' },
  })

  useEffect(() => {
    if (profileQuery.data) {
      form.reset(profileQuery.data)
    }
  }, [form, profileQuery.data])

  const updateMutation = useMutation({
    mutationFn: tenantApi.updateSettings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['tenant-profile'] })
    },
  })

  if (profileQuery.isLoading) return <div className="p-8"><LoadingState label="Carregando configurações..." /></div>
  if (profileQuery.isError) return <div className="p-8"><ErrorState detail="As configurações não puderam ser carregadas." retry={() => profileQuery.refetch()} /></div>

  return (
    <div className="p-6 md:p-8 space-y-6">
      {/* Header */}
      <div>
        <p className="text-xs font-bold uppercase tracking-wider text-primary">Controles do Tenant</p>
        <h1 className="text-3xl font-black tracking-tight text-slate-900">Configurações do Tenant</h1>
        <p className="mt-1 text-sm text-slate-500">
          Atualize contatos de cobrança, metadados e domínios. Alterações são restritas a administradores e auditadas.
        </p>
      </div>

      {!isAdmin ? (
        <AccessNotice
          title="Visualização somente leitura"
          detail="Você pode inspecionar o perfil do tenant, mas apenas administradores podem salvar alterações."
        />
      ) : null}

      <div className="rounded-xl border border-slate-200 bg-white p-6">
        <h2 className="text-lg font-bold text-slate-900 mb-6">Informações Gerais</h2>

        <form
          className="grid gap-5 md:grid-cols-2"
          onSubmit={form.handleSubmit((values) => updateMutation.mutate(values))}
        >
          <Field label="Nome do tenant" error={form.formState.errors.name?.message}>
            <Input {...form.register('name')} disabled={!isAdmin} />
          </Field>
          <Field label="E-mail de cobrança" error={form.formState.errors.billingEmail?.message}>
            <Input type="email" {...form.register('billingEmail')} disabled={!isAdmin} />
          </Field>
          <Field label="E-mail de suporte" error={form.formState.errors.supportEmail?.message}>
            <Input type="email" {...form.register('supportEmail')} disabled={!isAdmin} />
          </Field>
          <Field label="Fuso horário" error={form.formState.errors.timeZone?.message}>
            <Input {...form.register('timeZone')} disabled={!isAdmin} />
          </Field>
          <Field label="Tema" error={form.formState.errors.theme?.message}>
            <Input {...form.register('theme')} disabled={!isAdmin} />
          </Field>
          <div className="md:col-span-2">
            <Field
              label="Domínios permitidos"
              hint="Lista separada por vírgulas"
              error={form.formState.errors.allowedDomains?.message}
            >
              <Textarea rows={3} {...form.register('allowedDomains')} disabled={!isAdmin} />
            </Field>
          </div>

          {updateMutation.isError ? (
            <div className="md:col-span-2 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
              {problemMessage(updateMutation.error)}
            </div>
          ) : null}

          {updateMutation.isSuccess ? (
            <div className="md:col-span-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 font-medium">
              Configurações salvas com sucesso.
            </div>
          ) : null}

          <div className="flex justify-end gap-3 md:col-span-2">
            <Button
              type="button"
              variant="secondary"
              onClick={() => form.reset(profileQuery.data)}
              disabled={updateMutation.isPending}
            >
              Redefinir
            </Button>
            <Button disabled={!isAdmin || updateMutation.isPending} type="submit">
              {updateMutation.isPending ? 'Salvando...' : 'Salvar alterações'}
            </Button>
          </div>
        </form>
      </div>
    </div>
  )
}
