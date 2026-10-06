import { useEffect, useState } from 'react'

// Recheck on returning to a suspended/background tab as well as at the deadline.
export function useDeadline(value: string | undefined): boolean {
  const deadline = value ? Date.parse(value) : NaN
  const [now, setNow] = useState(Date.now)
  useEffect(() => {
    const update = () => setNow(Date.now())
    update()
    let timer: number | undefined
    const schedule = () => {
      if (Number.isFinite(deadline) && deadline > Date.now()) {
        timer = window.setTimeout(() => { update(); schedule() }, Math.min(deadline - Date.now(), 2_147_483_647))
      }
    }
    schedule()
    window.addEventListener('focus', update)
    document.addEventListener('visibilitychange', update)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('focus', update)
      document.removeEventListener('visibilitychange', update)
    }
  }, [deadline])
  return Number.isFinite(deadline) && now >= deadline
}
