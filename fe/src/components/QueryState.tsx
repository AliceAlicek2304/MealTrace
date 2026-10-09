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
  if (loading) return <p className={loadingClassName}>{loadingMessage}</p>
  if (error) return <p className={errorClassName}>{errorMessage}{onRetry && <> <button type="button" className="button secondary" onClick={onRetry}>Thử lại</button></>}</p>
  return children
}
