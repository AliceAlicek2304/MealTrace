// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, afterEach, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { StudentImportHistory } from './StudentImportHistory'
vi.mock('../../components/ClassPicker', () => ({ ClassPicker: () => <span>Lọc lớp</span> }))
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function(this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function(this: HTMLDialogElement) { this.removeAttribute('open') } })
})
afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('opens a batch and its original child profile in separate details without changing history', async () => {
  const batch = { id: 'batch-1', fileName: 'Danh sách.xlsx', sheetName: 'Sheet1', className: 'A3', schoolYear: '2026-2027', startDate: '2026-10-09', created: 1, importedAt: '2026-10-09T03:00:00Z', importedByName: 'Admin thử nghiệm' }
  const get = vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/batch-1') ? { batch, rows: [{ row: 4, fullName: 'Nguyễn An', dateOfBirth: '2021-03-04', gender: 'FEMALE' }] } : { items: [batch], total: 1 } }))
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={client}><StudentImportHistory /></QueryClientProvider>)
  fireEvent.click(await screen.findByRole('button', { name: /A3.*1 trẻ/ }))
  fireEvent.click(await screen.findByRole('button', { name: /Nguyễn An/ }))
  expect(screen.getByText('04/03/2021')).toBeTruthy()
  expect(screen.getByText('Nữ')).toBeTruthy()
  expect(get).toHaveBeenCalledWith('/admin/students/import/history/batch-1')
  client.clear()
})

it('shows an empty history clearly', async () => {
  vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], total: 0 } })
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={client}><StudentImportHistory /></QueryClientProvider>)
  expect(await screen.findByText('Chưa có lịch sử nhập phù hợp.')).toBeTruthy()
  client.clear()
})
