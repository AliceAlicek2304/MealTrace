// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { PortionsPage } from './PortionsPage'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() }, Toaster: () => null }))
let cache: QueryClient
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks(); vi.useRealTimers() })
describe('Portion settlement cutoff', () => {
  it('enables admin settlement at cutoff without reopening the session and keeps teacher read-only', async () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-06T00:29:00Z'))
    Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
    Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
    const day = { id: 'meal', date: '2026-10-06', mealType: 'Bữa trưa', schoolYear: '2026-2027', cutoffAt: '2026-10-06T00:30:00Z', isSettled: false, isCancelled: false, cancellationReason: null }
    vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/workflow') ? { items: [day], total: 1 } : { ...day, classes: [] } }))
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const renderPage = (roles: string[]) => render(<QueryClientProvider client={cache}><PortionsPage roles={roles} /></QueryClientProvider>)
    const admin = renderPage(['ADMIN'])
    await act(async () => { await vi.advanceTimersByTimeAsync(1) })
    fireEvent.click(screen.getByRole('button', { name: 'Xem / Quản lý' }))
    await act(async () => { await vi.advanceTimersByTimeAsync(1) })
    expect((screen.getByRole('button', { name: 'Chốt và gửi bếp' }) as HTMLButtonElement).disabled).toBe(true)
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000) })
    expect((screen.getByRole('button', { name: 'Chốt và gửi bếp' }) as HTMLButtonElement).disabled).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Chốt và gửi bếp' }))
    expect(screen.getByRole('dialog', { name: 'Xác nhận chốt suất' })).toBeTruthy()
    admin.unmount()
    renderPage(['TEACHER'])
    fireEvent.click(screen.getByRole('button', { name: 'Xem / Quản lý' }))
    await act(async () => { await vi.advanceTimersByTimeAsync(1) })
    expect(screen.queryByRole('button', { name: 'Chốt và gửi bếp' })).toBeNull()
  })
})
