// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { schoolToday } from '../../lib/schoolTime'
import { AbsencesPage } from './AbsencesPage'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() }, Toaster: () => null }))
let cache: QueryClient
afterEach(() => { cleanup(); cache?.clear(); vi.restoreAllMocks() })

it('prevents submitting a closed school day while allowing a period with another serving day', async () => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  const today = schoolToday()
  const tomorrow = new Date(Date.parse(`${today}T00:00:00Z`) + 86400000).toISOString().slice(0, 10)
  const child = { studentId: 'child', fullName: 'Nguyễn An', className: 'A3', schoolYear: '2026-2027', yearStartDate: today, yearEndDate: tomorrow, unavailableMealDates: [today] }
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url === '/parent/students' ? [child] : url === '/academic-years' ? [{ code: child.schoolYear, startDate: today, endDate: tomorrow }] : { items: [], total: 0, students: [] } }))
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={cache}><AbsencesPage /></QueryClientProvider>)
  fireEvent.click(await screen.findByRole('button', { name: 'Đăng ký không ăn' }))
  const dialog = within(screen.getByRole('dialog', { name: 'Đăng ký không ăn' }))
  await dialog.findByRole('option', { name: 'Nguyễn An · A3' })
  fireEvent.change(dialog.getByLabelText('Trẻ'), { target: { value: 'child' } })
  expect((dialog.getByRole('button', { name: 'Gửi đăng ký' }) as HTMLButtonElement).disabled).toBe(true)
  expect(dialog.getByRole('status').textContent).toContain('Nhà trường không phục vụ ăn')
  fireEvent.submit(dialog.getByLabelText('Lý do').closest('form')!)
  expect(post).not.toHaveBeenCalled()
  fireEvent.change(dialog.getByLabelText('Đến ngày'), { target: { value: tomorrow } })
  expect((dialog.getByRole('button', { name: 'Gửi đăng ký' }) as HTMLButtonElement).disabled).toBe(false)
  expect(dialog.queryByRole('status')).toBeNull()
})
