import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { problemMessage, loginRequest } from '../../lib/http'
import { useSession } from './use-session'

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const loginSchema = z.object({
  email: z.string().email('E-mail inválido'),
  password: z.string().min(8, 'Mínimo 8 caracteres'),
  tenantId: z.string().regex(guidPattern, 'Formato GUID inválido'),
})

type LoginFormValues = z.infer<typeof loginSchema>

const demoAccounts = [
  {
    name: 'Acme Operations',
    tenantId: '11111111-1111-1111-1111-111111111111',
    email: 'admin@acme.test',
    role: 'Admin',
  },
  {
    name: 'Globex Advisory',
    tenantId: '22222222-2222-2222-2222-222222222222',
    email: 'admin@globex.test',
    role: 'Admin',
  },
] as const

export function LoginPage() {
  const navigate = useNavigate()
  const location = useLocation()
  const { session } = useSession()
  const redirectPath = (location.state as { from?: string } | null)?.from ?? '/'
  const [values, setValues] = useState<LoginFormValues>({
    email: demoAccounts[0].email,
    password: 'Passw0rd!',
    tenantId: demoAccounts[0].tenantId,
  })
  const [errors, setErrors] = useState<Partial<Record<keyof LoginFormValues, string>>>({})
  const [showPassword, setShowPassword] = useState(false)

  const loginMutation = useMutation({
    mutationFn: async (values: LoginFormValues) => loginRequest(values.email, values.password, values.tenantId),
    onSuccess: () => {
      navigate(redirectPath, { replace: true })
    },
  })

  function updateField(field: keyof LoginFormValues, value: string) {
    setValues((current) => ({ ...current, [field]: value }))
    setErrors((current) => ({ ...current, [field]: undefined }))
  }

  function submitForm() {
    const result = loginSchema.safeParse(values)
    if (!result.success) {
      const nextErrors: Partial<Record<keyof LoginFormValues, string>> = {}
      for (const issue of result.error.issues) {
        const key = issue.path[0] as keyof LoginFormValues | undefined
        if (key && !nextErrors[key]) {
          nextErrors[key] = issue.message
        }
      }
      setErrors(nextErrors)
      return
    }
    loginMutation.mutate(result.data)
  }

  if (session) {
    return <Navigate to="/" replace />
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-background-light p-4 font-display">
      <div className="flex w-full max-w-[1100px] min-h-[700px] overflow-hidden rounded-2xl shadow-2xl shadow-primary/10 border border-slate-200 bg-white">

        {/* Left panel — brand */}
        <div className="relative hidden lg:flex w-5/12 flex-col justify-between overflow-hidden bg-primary p-12">
          <div className="absolute inset-0 pointer-events-none opacity-10 bg-[radial-gradient(circle_at_50%_50%,_#fff_0%,_transparent_70%)]" />
          <div className="absolute -bottom-20 -right-20 h-80 w-80 rounded-full bg-white/10 blur-3xl" />

          <div className="relative z-10 flex items-center gap-3 text-white">
            <span className="material-symbols-outlined text-3xl">deployed_code</span>
            <h1 className="text-2xl font-black tracking-tight">TenantCore</h1>
          </div>

          <div className="relative z-10">
            <h2 className="text-4xl font-black leading-tight text-white mb-5">
              Plataforma Multi-Tenant Elevada ao Próximo Nível
            </h2>
            <p className="text-white/80 text-base leading-relaxed max-w-sm">
              Gerencie múltiplos tenants, projetos, clientes e usuários com isolamento real, auditoria e controle de cotas.
            </p>

            <div className="mt-10 flex gap-4">
              <div className="flex-1 rounded-xl border border-white/20 bg-white/10 p-4 backdrop-blur-md">
                <span className="material-symbols-outlined text-white mb-2 block">shield_lock</span>
                <p className="text-sm font-bold text-white">Isolamento Tenant</p>
              </div>
              <div className="flex-1 rounded-xl border border-white/20 bg-white/10 p-4 backdrop-blur-md">
                <span className="material-symbols-outlined text-white mb-2 block">analytics</span>
                <p className="text-sm font-bold text-white">Auditoria em Tempo Real</p>
              </div>
            </div>

            {/* Demo accounts */}
            <div className="mt-10">
              <p className="text-xs font-bold uppercase tracking-widest text-white/60 mb-3">Contas de demonstração</p>
              <div className="space-y-2">
                {demoAccounts.map((account) => (
                  <button
                    key={account.tenantId}
                    type="button"
                    onClick={() => {
                      setValues({ email: account.email, tenantId: account.tenantId, password: 'Passw0rd!' })
                      setErrors({})
                    }}
                    className="w-full rounded-xl border border-white/20 bg-white/10 p-3 text-left transition hover:bg-white/20"
                  >
                    <p className="text-sm font-bold text-white">{account.name}</p>
                    <p className="text-xs text-white/70 mt-0.5">{account.email}</p>
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="relative z-10 text-white/50 text-xs">
            © 2024 TenantCore. Todos os direitos reservados.
          </div>
        </div>

        {/* Right panel — form */}
        <div className="flex flex-1 flex-col justify-center p-8 md:p-14">
          <div className="mb-8">
            <h3 className="text-3xl font-black text-slate-900 mb-1">Bem-vindo de volta</h3>
            <p className="text-slate-500 text-sm">Acesse sua conta com as credenciais abaixo.</p>
          </div>

          <form
            className="space-y-5"
            onSubmit={(event) => {
              event.preventDefault()
              submitForm()
            }}
          >
            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-slate-700">E-mail</label>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 text-xl">mail</span>
                <input
                  type="email"
                  placeholder="admin@empresa.com"
                  value={values.email}
                  onChange={(e) => updateField('email', e.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 py-3.5 pl-11 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
              </div>
              {errors.email ? <p className="text-xs text-red-500">{errors.email}</p> : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-slate-700">Senha</label>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 text-xl">lock</span>
                <input
                  type={showPassword ? 'text' : 'password'}
                  placeholder="••••••••••••"
                  value={values.password}
                  onChange={(e) => updateField('password', e.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 py-3.5 pl-11 pr-12 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-3.5 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
                >
                  <span className="material-symbols-outlined text-xl">
                    {showPassword ? 'visibility_off' : 'visibility'}
                  </span>
                </button>
              </div>
              {errors.password ? <p className="text-xs text-red-500">{errors.password}</p> : null}
            </div>

            <div className="flex flex-col gap-1.5">
              <label className="text-sm font-semibold text-slate-700">ID do Tenant</label>
              <div className="relative">
                <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-slate-400 text-xl">domain</span>
                <input
                  type="text"
                  placeholder="11111111-1111-1111-1111-111111111111"
                  value={values.tenantId}
                  onChange={(e) => updateField('tenantId', e.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-slate-50 py-3.5 pl-11 pr-4 text-sm text-slate-900 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/20 font-mono"
                />
              </div>
              {errors.tenantId ? <p className="text-xs text-red-500">{errors.tenantId}</p> : null}
            </div>

            {loginMutation.isError ? (
              <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">
                {problemMessage(loginMutation.error)}
              </div>
            ) : null}

            <button
              type="submit"
              disabled={loginMutation.isPending}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-primary py-4 text-sm font-bold text-white shadow-lg shadow-primary/20 transition active:scale-[0.98] hover:bg-primary/90 disabled:opacity-50"
            >
              <span>{loginMutation.isPending ? 'Entrando...' : 'Entrar no Dashboard'}</span>
              <span className="material-symbols-outlined text-[18px]">login</span>
            </button>
          </form>

          <p className="mt-8 text-center text-sm text-slate-500">
            Senha padrão para todas as contas:{' '}
            <span className="font-bold text-slate-900">Passw0rd!</span>
          </p>
        </div>
      </div>
    </div>
  )
}
