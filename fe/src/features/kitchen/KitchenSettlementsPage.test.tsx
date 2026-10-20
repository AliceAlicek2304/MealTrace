// @vitest-environment jsdom
import { beforeEach, afterEach, it, expect, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { toast } from 'sonner'
import { api } from '../../lib/api'
import { canOpenPage } from '../../lib/navigation'
import { KitchenSettlementsPage } from './KitchenSettlementsPage'

vi.mock('sonner', () => ({ Toaster: () => null, toast: { success: vi.fn(), error: vi.fn() } }))
let cache: QueryClient
const token = 'a'.repeat(64)
const day = { mealDayId: 'day-1', date: '2026-10-19', sessionType: 'Bữa trưa', originalPortions: 30, appliedPortions: 32, diff: 2, isAcknowledged: false }
const detail = { appliedTotal: 32, originalTotal: 30, totalDiff: 2, snapshotToken: token, isAcknowledged: false, classDiffs: [{ classId: 'class-1', className: 'A3', originalCount: 30, appliedCount: 32, kitchenAdjustment: 2, currentVersion: 2, hasCompleteRoster: false, addedStudents: [], removedStudents: [] }] }
beforeEach(() => {
  vi.clearAllMocks()
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
function mount() { render(<QueryClientProvider client={cache}><KitchenSettlementsPage /></QueryClientProvider>) }
it('confirms only the displayed snapshot after explicit review and shows saved history', async () => {
  let saved = false
  vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url.endsWith('/search')) return { data: { items: [day], total: 1 } }
    if (url.endsWith('/diff')) return { data: { ...detail, isAcknowledged: saved } }
    return { data: saved ? [{ id: 'ack-1', portionsCount: 32, acknowledgedAt: '2026-10-19T01:00:00Z', acknowledgedByName: 'Bếp A', snapshotToken: token, note: 'Đã kiểm tra' }] : [] }
  })
  const post = vi.spyOn(api, 'post').mockImplementation(async () => { saved = true; return { data: {} } })
  mount()
  fireEvent.click(await screen.findByText('Đối chiếu / tiếp nhận'))
  await screen.findByText('Chưa đủ dữ liệu lịch sử để đối chiếu trẻ')
  const confirm = screen.getByRole('button', { name: 'Xác nhận tiếp nhận' })
  expect(confirm.hasAttribute('disabled')).toBe(true)
  fireEvent.click(screen.getByRole('checkbox'))
  fireEvent.change(screen.getByLabelText('Ghi chú tiếp nhận'), { target: { value: 'Đã kiểm tra' } })
  fireEvent.click(confirm)
  await screen.findByText('Bếp đã tiếp nhận bản suất hiện hành.')
  expect(post).toHaveBeenCalledExactlyOnceWith('/kitchen/settlements/day-1/acknowledge', { snapshotToken: token, note: 'Đã kiểm tra' })
  expect(await screen.findByText('32 suất · Bếp A')).toBeTruthy()
})
it('stale confirmation reloads the snapshot and requires another review', async () => {
  let stale = false
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/search') ? { items: [day], total: 1 } : url.endsWith('/diff') ? { ...detail, snapshotToken: stale ? 'b'.repeat(64) : token } : [] }))
  vi.spyOn(api, 'post').mockImplementation(async () => {
    stale = true
    const error = new AxiosError('stale')
    error.response = { status: 409, data: { detail: 'Bản suất đã thay đổi.' } } as NonNullable<AxiosError['response']>
    throw error
  })
  mount()
  fireEvent.click(await screen.findByText('Đối chiếu / tiếp nhận'))
  await screen.findByRole('checkbox')
  fireEvent.click(screen.getByRole('checkbox'))
  fireEvent.click(screen.getByRole('button', { name: 'Xác nhận tiếp nhận' }))
  await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Bản suất đã thay đổi.', { toasterId: 'edit-modal' }))
  await waitFor(() => expect((screen.getByRole('checkbox') as HTMLInputElement).checked).toBe(false))
  expect(screen.getByRole('button', { name: 'Xác nhận tiếp nhận' }).hasAttribute('disabled')).toBe(true)
})
it('kitchen navigation is restricted to school managers and kitchen staff', () => {
  expect(canOpenPage('kitchen', ['ADMIN'])).toBe(true)
  expect(canOpenPage('kitchen', ['KITCHEN_STAFF'])).toBe(true)
  expect(canOpenPage('kitchen', ['PARENT'])).toBe(false)
  expect(canOpenPage('kitchen', ['TEACHER'])).toBe(false)
})
