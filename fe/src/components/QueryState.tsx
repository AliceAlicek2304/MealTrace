import type { ReactNode } from 'react'

type Props = Readonly<{
  loading: boolean
  error: boolean
  loadingMessage: string
  errorMessage: string
  loadingClassName?: string
  errorClassName?: string
  onRetry?: () => void
  children: ReactNode
}>

export function QueryState({ loading, error, loadingMessage, errorMessage, loadingClassName = 'empty compact', errorClassName = 'empty compact error', onRetry, children }: Props) {
  if (loading) return <output aria-live="polite" className={loadingClassName}>{loadingMessage}</output>
  if (error) return <div role="alert" className={errorClassName}><p>{errorMessage}</p>{onRetry && <button type="button" className="button secondary" onClick={onRetry}>Thử lại</button>}</div>
  return children
}
