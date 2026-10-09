import { useEffect, useState } from 'react'

export type Page = 'links' | 'accounts' | 'classes' | 'calendar' | 'portions' | 'absences' | 'meals' | 'profile'
const permissions: Record<Page, string[]> = {
  links: ['ADMIN', 'TEACHER', 'PARENT'], accounts: ['ADMIN'], classes: ['ADMIN', 'TEACHER'], calendar: ['ADMIN'],
  portions: ['ADMIN', 'TEACHER', 'KITCHEN_STAFF'], absences: ['PARENT'],
  meals: ['ADMIN', 'KITCHEN_STAFF'], profile: [],
}
export function pageFromHash(): Page | null {
  const path = window.location.hash.slice(2)
  return window.location.hash.startsWith('#/') && Object.hasOwn(permissions, path) ? path as Page : null
}
export function canOpenPage(page: Page, roles: string[]) {
  return page === 'profile' || permissions[page].some(role => roles.includes(role))
}
export function defaultPage(roles: string[]): Page {
  return roles.includes('ADMIN') ? 'portions' : roles.includes('PARENT') ? 'absences'
    : roles.some(role => ['TEACHER', 'KITCHEN_STAFF'].includes(role)) ? 'portions' : 'profile'
}
export function usePageNavigation() {
  const [page, setPage] = useState<Page | null>(pageFromHash)
  useEffect(() => {
    const sync = () => setPage(pageFromHash())
    window.addEventListener('hashchange', sync)
    return () => window.removeEventListener('hashchange', sync)
  }, [])
  function navigate(next: Page, replace = false) {
    if (replace) window.history.replaceState(null, '', `#/${next}`)
    else if (window.location.hash !== `#/${next}`) window.location.hash = `/${next}`
    setPage(next)
  }
  return [page, navigate] as const
}
