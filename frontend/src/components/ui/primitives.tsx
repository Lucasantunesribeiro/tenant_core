import clsx from 'clsx'
import { forwardRef } from 'react'
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  PropsWithChildren,
  ReactNode,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from 'react'

export function SectionHeader({
  eyebrow,
  title,
  description,
  actions,
}: {
  eyebrow?: string
  title: string
  description?: string
  actions?: ReactNode
}) {
  return (
    <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
      <div className="space-y-1">
        {eyebrow ? <p className="text-xs font-bold uppercase tracking-wider text-primary">{eyebrow}</p> : null}
        <h1 className="text-3xl font-black tracking-tight text-slate-900 dark:text-white">{title}</h1>
        {description ? <p className="max-w-3xl text-sm text-slate-500 dark:text-slate-400">{description}</p> : null}
      </div>
      {actions ? <div className="flex flex-wrap items-center gap-3">{actions}</div> : null}
    </div>
  )
}

export function Panel({ className, children }: PropsWithChildren<{ className?: string }>) {
  return (
    <section className={clsx('rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6', className)}>
      {children}
    </section>
  )
}

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'

const buttonVariants: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-white hover:bg-primary/90 shadow-sm shadow-primary/20',
  secondary: 'border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-800',
  ghost: 'text-slate-600 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-800',
  danger: 'bg-red-500 text-white hover:bg-red-600',
}

export function Button({
  variant = 'primary',
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: ButtonVariant }) {
  return (
    <button
      className={clsx(
        'inline-flex items-center justify-center rounded-xl px-4 py-2 text-sm font-semibold transition focus:outline-none focus:ring-2 focus:ring-primary/30 disabled:cursor-not-allowed disabled:opacity-50',
        buttonVariants[variant],
        className,
      )}
      {...props}
    />
  )
}

function fieldBase(className?: string) {
  return clsx(
    'w-full rounded-lg border border-slate-300 dark:border-slate-700 bg-white dark:bg-slate-800 px-3 py-2.5 text-sm text-slate-900 dark:text-slate-100 outline-none transition placeholder:text-slate-400 focus:border-primary focus:ring-2 focus:ring-primary/20',
    className,
  )
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(function Input(props, ref) {
  return <input ref={ref} className={fieldBase(props.className)} {...props} />
})

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(function Select(props, ref) {
  return <select ref={ref} className={fieldBase(props.className)} {...props} />
})

export const Textarea = forwardRef<HTMLTextAreaElement, TextareaHTMLAttributes<HTMLTextAreaElement>>(function Textarea(props, ref) {
  return <textarea ref={ref} className={fieldBase(props.className)} {...props} />
})

export function Field({
  label,
  hint,
  error,
  children,
}: PropsWithChildren<{ label: string; hint?: string; error?: string }>) {
  return (
    <label className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between gap-3">
        <span className="text-sm font-semibold text-slate-700 dark:text-slate-300">{label}</span>
        {hint ? <span className="text-xs text-slate-400">{hint}</span> : null}
      </div>
      {children}
      {error ? <p className="text-xs text-red-500">{error}</p> : null}
    </label>
  )
}

type BadgeTone = 'default' | 'success' | 'warning' | 'danger' | 'info'

const badgeTones: Record<BadgeTone, string> = {
  default: 'border-slate-200 dark:border-slate-700 bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300',
  success: 'border-emerald-200 dark:border-emerald-800 bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400',
  warning: 'border-amber-200 dark:border-amber-800 bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400',
  danger: 'border-red-200 dark:border-red-800 bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400',
  info: 'border-blue-200 dark:border-blue-800 bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400',
}

export function Badge({ tone = 'default', children }: PropsWithChildren<{ tone?: BadgeTone }>) {
  return (
    <span className={clsx('inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-bold', badgeTones[tone])}>
      {children}
    </span>
  )
}

export function MetricCard({
  label,
  value,
  accent: _accent,
  detail,
}: {
  label: string
  value: string | number
  accent: string
  detail?: string
}) {
  return (
    <div className="relative overflow-hidden rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6">
      <div className="absolute right-0 top-0 -mr-8 -mt-8 h-24 w-24 rounded-full bg-primary/5" />
      <p className="text-sm font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-2 text-4xl font-black text-slate-900 dark:text-white">{value}</p>
      {detail ? <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">{detail}</p> : null}
    </div>
  )
}

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string
  description: string
  action?: React.ReactNode
}) {
  return (
    <div className="flex min-h-56 flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-8 text-center">
      <h3 className="text-lg font-bold text-slate-900 dark:text-white">{title}</h3>
      <p className="max-w-md text-sm text-slate-500 dark:text-slate-400">{description}</p>
      {action ? <div className="pt-2">{action}</div> : null}
    </div>
  )
}

export function LoadingState({ label = 'Carregando dados...' }: { label?: string }) {
  return (
    <div className="flex min-h-40 items-center justify-center gap-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 p-6">
      <div className="h-3 w-3 animate-pulse rounded-full bg-primary" />
      <p className="text-sm text-slate-500 dark:text-slate-400">{label}</p>
    </div>
  )
}

export function ErrorState({
  title = 'Erro ao carregar',
  detail,
  retry,
}: {
  title?: string
  detail: string
  retry?: () => void
}) {
  return (
    <div className="rounded-xl border border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-950/20 p-6">
      <div className="space-y-2">
        <h3 className="text-base font-bold text-red-700 dark:text-red-400">{title}</h3>
        <p className="text-sm text-red-600 dark:text-red-300">{detail}</p>
        {retry ? (
          <div className="pt-2">
            <Button variant="danger" onClick={retry}>
              Tentar novamente
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  )
}

export function AccessNotice({ title, detail }: { title: string; detail: string }) {
  return (
    <div className="rounded-xl border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-950/20 p-5">
      <h3 className="text-base font-bold text-amber-700 dark:text-amber-400">{title}</h3>
      <p className="mt-1 text-sm text-amber-600 dark:text-amber-300">{detail}</p>
    </div>
  )
}

export function Modal({
  open,
  title,
  description,
  children,
  onClose,
}: PropsWithChildren<{
  open: boolean
  title: string
  description?: string
  onClose: () => void
}>) {
  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-2xl rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 p-6 shadow-2xl">
        <div className="mb-6 flex items-start justify-between gap-4">
          <div className="space-y-1">
            <h2 className="text-xl font-bold text-slate-900 dark:text-white">{title}</h2>
            {description ? <p className="text-sm text-slate-500 dark:text-slate-400">{description}</p> : null}
          </div>
          <Button variant="ghost" onClick={onClose}>
            Fechar
          </Button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function DataTable({ children }: PropsWithChildren) {
  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 shadow-sm">
      {children}
    </div>
  )
}

export function Table({ children }: PropsWithChildren) {
  return <table className="w-full divide-y divide-slate-100 dark:divide-slate-800 text-left text-sm">{children}</table>
}

export function Th({ children, className }: PropsWithChildren<{ className?: string }>) {
  return (
    <th className={clsx('bg-slate-50 dark:bg-slate-800/50 px-6 py-4 text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400', className)}>
      {children}
    </th>
  )
}

export function Td({ children, className }: PropsWithChildren<{ className?: string }>) {
  return <td className={clsx('px-6 py-4 align-top text-slate-700 dark:text-slate-300', className)}>{children}</td>
}
