// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { AdminToolsPage } from './AdminToolsPage'
vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))
afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})
it('reads audit detail from the API and never sends a test message on load', async () => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.setAttribute('open', '')
    },
  })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.removeAttribute('open')
    },
  })
  const get = vi.spyOn(api, 'get').mockImplementation(async (path) => ({
    data:
      path === '/notifications/settings'
        ? { enabled: true }
        : path === '/admin/audit-logs/source%3A1'
          ? { reason: 'Đối chiếu hồ sơ' }
          : {
              items: [
                {
                  id: 'source:1',
                  action: 'UPDATE',
                  occurredAt: '2026-11-14T00:00:00Z',
                  entityType: 'Student',
                },
              ],
              total: 1,
            },
  }))
  const post = vi.spyOn(api, 'post')
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={cache}>
      <AdminToolsPage />
    </QueryClientProvider>,
  )
  fireEvent.click(await screen.findByRole('button', { name: 'Xem' }))
  expect(await screen.findByText('Đối chiếu hồ sơ')).toBeTruthy()
  expect(get).toHaveBeenCalledWith('/admin/audit-logs/source%3A1')
  expect(post).not.toHaveBeenCalled()
  cache.clear()
})
