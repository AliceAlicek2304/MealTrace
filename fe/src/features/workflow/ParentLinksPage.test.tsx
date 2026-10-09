// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { ParentLinksPage } from './ParentLinksPage'
let cache: QueryClient
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
function mount(roles: string[]) { render(<QueryClientProvider client={cache}><ParentLinksPage roles={roles} /></QueryClientProvider>) }
it('parent chooses school year and class and submits child name without granting access or supplying an account id', async () => {
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/classes') ? [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] : { items: [], total: 0 } }))
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: null })
  mount(['PARENT'])
  fireEvent.click(screen.getByText('Yêu cầu liên kết trẻ'))
  await screen.findByRole('option', { name: '2026-2027' })
  fireEvent.change(screen.getByLabelText('Năm học của trẻ'), { target: { value: '2026-2027' } })
  await screen.findByRole('option', { name: 'A3' })
  fireEvent.change(screen.getByLabelText('Lớp của trẻ'), { target: { value: 'class-1' } })
  fireEvent.change(screen.getByLabelText('Họ tên trẻ'), { target: { value: 'Nguyễn An' } })
  fireEvent.change(screen.getByLabelText('Quan hệ'), { target: { value: 'MOTHER' } })
  fireEvent.click(screen.getByText('Xác nhận'))
  await waitFor(() => expect(post).toHaveBeenCalledExactlyOnceWith('/parent/link-requests', { classId: 'class-1', studentCode: null, studentName: 'Nguyễn An', relationship: 'MOTHER', note: '' }))
  expect(screen.queryByText('Duyệt / từ chối')).toBeNull()
})
it('school reviews a pending request with revision and verification reason', async () => {
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/classes') ? [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] : { items: [{ id: 'request-1', studentCode: 'HS-001', studentName: 'Nguyễn An', relationship: 'MOTHER', note: '', status: 'PENDING', requestedAt: '2026-10-09T01:00:00Z', revision: 2, parentName: 'Mẹ An', parentPhone: '0901234567', className: 'M1' }], total: 1 } }))
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: null })
  mount(['TEACHER'])
  fireEvent.click(await screen.findByText('Duyệt / từ chối'))
  expect(screen.queryByLabelText('Mã trẻ do nhà trường cung cấp')).toBeNull()
  fireEvent.change(screen.getByLabelText('Quyết định'), { target: { value: 'true' } })
  fireEvent.change(screen.getByLabelText('Kết quả đối chiếu / lý do'), { target: { value: 'Đã đối chiếu hồ sơ' } })
  fireEvent.click(screen.getByText('Xác nhận'))
  await waitFor(() => expect(post).toHaveBeenCalledExactlyOnceWith('/student-link-requests/request-1/review', { revision: 2, approve: true, reason: 'Đã đối chiếu hồ sơ' }))
})

it('teacher selects a class and sends only checked requests with captured revisions', async () => {
  const rows = ['An', 'Bình'].map((name, index) => ({ id: `request-${index}`, studentCode: `HS-${index}`, studentName: name, relationship: 'MOTHER', note: '', status: 'PENDING', requestedAt: '2026-10-09T01:00:00Z', revision: index, parentName: `Mẹ ${name}`, parentPhone: '0901234567', className: 'A3', classId: 'class-1' }))
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/classes') ? [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] : { items: rows, total: 2 } }))
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: null })
  mount(['TEACHER'])
  await screen.findByRole('option', { name: '2026-2027' })
  fireEvent.change(screen.getByLabelText('Năm học'), { target: { value: '2026-2027' } })
  fireEvent.change(screen.getByLabelText('Lớp phụ trách'), { target: { value: 'class-1' } })
  await screen.findByLabelText('Chọn An - Mẹ An')
  await waitFor(() => expect(screen.getByText('Chọn yêu cầu chờ trên trang').hasAttribute('disabled')).toBe(false))
  fireEvent.click(screen.getByText('Chọn yêu cầu chờ trên trang'))
  fireEvent.click(screen.getByLabelText('Chọn An - Mẹ An'))
  fireEvent.click(screen.getByText('Xử lý 1 yêu cầu đã chọn'))
  fireEvent.change(screen.getByLabelText('Quyết định'), { target: { value: 'true' } })
  fireEvent.change(screen.getByLabelText('Kết quả đối chiếu / lý do'), { target: { value: 'Đã rà danh sách lớp' } })
  fireEvent.click(screen.getByText('Xác nhận'))
  await waitFor(() => expect(post).toHaveBeenCalledExactlyOnceWith('/student-link-requests/bulk-review', { classId: 'class-1', schoolYear: '2026-2027', items: [{ id: 'request-1', revision: 1 }], approve: true, reason: 'Đã rà danh sách lớp' }))
})
