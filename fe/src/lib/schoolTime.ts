export const schoolTimeZone = 'Asia/Ho_Chi_Minh'

export function earlierSchoolDate(left: string, right: string): string {
  if (left < right) return left
  return right
}

export function laterSchoolDate(left: string, right: string): string {
  if (left > right) return left
  return right
}

export function schoolToday(now: Date = new Date()): string {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: schoolTimeZone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(now)
  const part = (type: string) => parts.find(value => value.type === type)!.value
  return `${part('year')}-${part('month')}-${part('day')}`
}

export function schoolDateTime(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('vi-VN', { timeZone: schoolTimeZone })
}

export function schoolTime(value: string): string {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleTimeString('vi-VN', { timeZone: schoolTimeZone, hour: '2-digit', minute: '2-digit' })
}
