import type { ReactNode } from 'react'

type Props = Readonly<{
  loading: boolean
  error: boolean
  loadingMessage: string
  errorMessage: string
  loadingClassName?: string
  errorClassName?: string
  children: ReactNode
}>

export function QueryState({ loading, error, loadingMessage, errorMessage, loadingClassName = 'empty compact', errorClassName = 'empty compact error', children }: Props) {
  if (loading) return <p className={loadingClassName}>{loadingMessage}</p>
  if (error) return <p className={errorClassName}>{errorMessage}</p>
  return <>{children}</>
}
