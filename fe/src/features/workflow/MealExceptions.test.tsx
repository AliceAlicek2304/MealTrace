// @vitest-environment jsdom
import { act, cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { MealExceptions } from './MealExceptions'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() }, Toaster: () => null }))
let cache: QueryClient
beforeEach(() => {
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks(); vi.useRealTimers() })
const student = { studentId: 'child', studentCode: 'HS-1', fullName: 'Trẻ A', classId: 'class', className: 'M1', willEat: true,
  source: 'DEFAULT', parentReportedAbsent: false, latestEventId: null, latestAction: null, latestReason: null }
function show() { return render(<QueryClientProvider client={cache}><MealExceptions mealId="meal" /></QueryClientProvider>) }

describe('Meal decision UI', () => {
  it('shows the original source alongside the approved current portion and kitchen total', async () => {
    vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/portions')
      ? { classes: [{ classId: 'class', className: 'M1', studentIds: ['other-1', 'other-2'], isSettled: true, version: 2, count: 2, kitchenAdjustment: 0 }] }
      : { items: [student], total: 1, canEdit: false, isSettled: true, isCancelled: false, cutoffAt: '2026-10-06T00:30:00Z', classes: [] } }))
    show()
    const row = (await screen.findByText('Trẻ A')).closest('tr')!
    await waitFor(() => expect(within(row).getByText('Không có suất')).toBeTruthy())
    expect(within(row).getByText('Có suất')).toBeTruthy()
    expect(within(row).getByText('Bản 2 · Đã điều chỉnh')).toBeTruthy()
    expect(screen.getByText('Đang gửi bếp: 2 suất')).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Ghi ngoại lệ' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('disables an already-open editor exactly at cutoff', async () => {
    const cutoff = new Date(Date.now() + 60_000).toISOString()
    vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/exceptions') ? { items: [], total: 0 }
      : { items: [student], total: 1, canEdit: true, isSettled: false, isCancelled: false, cutoffAt: cutoff, classes: [] } }))
    const post = vi.spyOn(api, 'post')
    const user = userEvent.setup()
    show()
    await user.click(await screen.findByRole('button', { name: 'Ghi ngoại lệ' }))
    await user.type(screen.getByLabelText('Lý do'), 'Đón sớm')
    expect((screen.getByRole('button', { name: 'Lưu ngoại lệ' }) as HTMLButtonElement).disabled).toBe(false)
    vi.useFakeTimers()
    act(() => {
      vi.setSystemTime(new Date(cutoff))
      window.dispatchEvent(new Event('focus'))
    })
    expect((screen.getByRole('button', { name: 'Lưu ngoại lệ' }) as HTMLButtonElement).disabled).toBe(true)
    expect(screen.getByText('Đã hết thời gian ghi ngoại lệ. Bản suất giữ nguyên.')).toBeTruthy()
    expect(post).not.toHaveBeenCalled()
  })
})
