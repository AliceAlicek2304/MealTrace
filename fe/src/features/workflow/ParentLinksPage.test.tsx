// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { ParentLinksPage } from './ParentLinksPage'
import { toast } from 'sonner'
import { AxiosError } from 'axios'
vi.mock('sonner', () => ({ Toaster: () => null, toast: { success: vi.fn(), error: vi.fn() } }))
let cache: QueryClient
beforeEach(() => {
  vi.clearAllMocks()
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
  await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Đã gửi yêu cầu liên kết. Vui lòng chờ nhà trường duyệt.'))
  expect(screen.queryByText('Duyệt / từ chối')).toBeNull()
})

it('parent can retry loading class choices from the request modal', async () => {
  let available = false
  vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url.endsWith('/classes')) {
      if (!available) throw new Error('offline')
      return { data: [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] }
    }
    return { data: { items: [], total: 0 } }
  })
  mount(['PARENT'])
  fireEvent.click(screen.getByText('Yêu cầu liên kết trẻ'))
  const retry = await screen.findByRole('button', { name: 'Thử lại danh sách lớp' })
  available = true
  fireEvent.click(retry)
  await screen.findByRole('option', { name: '2026-2027' })
  expect(screen.queryByRole('button', { name: 'Thử lại danh sách lớp' })).toBeNull()
})

it('failed submission reports a modal toast and preserves the parent draft', async () => {
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/classes') ? [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] : { items: [], total: 0 } }))
  vi.spyOn(api, 'post').mockRejectedValue(new Error('offline'))
  mount(['PARENT'])
  fireEvent.click(screen.getByText('Yêu cầu liên kết trẻ'))
  await screen.findByRole('option', { name: '2026-2027' })
  fireEvent.change(screen.getByLabelText('Năm học của trẻ'), { target: { value: '2026-2027' } })
  fireEvent.change(screen.getByLabelText('Lớp của trẻ'), { target: { value: 'class-1' } })
  fireEvent.change(screen.getByLabelText('Họ tên trẻ'), { target: { value: 'Nguyễn An' } })
  fireEvent.click(screen.getByText('Xác nhận'))
  await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.any(String), { toasterId: 'edit-modal' }))
  expect((screen.getByLabelText('Họ tên trẻ') as HTMLInputElement).value).toBe('Nguyễn An')
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

it('concurrent review refreshes the list and clears obsolete selections', async () => {
  const row = { id: 'request-1', studentCode: 'HS-001', studentName: 'Nguyễn An', relationship: 'MOTHER', note: '', status: 'PENDING', revision: 2, requestedAt: '2026-10-09T01:00:00Z', parentName: 'Mẹ An', parentPhone: '0901234567', className: 'A3' }
  let processed = false
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('/classes') ? [{ id: 'class-1', name: 'A3', schoolYear: '2026-2027' }] : { items: processed ? [] : [row], total: processed ? 0 : 1 } }))
  vi.spyOn(api, 'post').mockImplementation(async () => {
    processed = true
    const error = new AxiosError('conflict')
    error.response = { status: 409, statusText: 'Conflict', data: { message: 'Yêu cầu đã được xử lý.' }, headers: {}, config: { headers: {} } } as NonNullable<AxiosError['response']>
    throw error
  })
  mount(['TEACHER'])
  await screen.findByRole('option', { name: '2026-2027' })
  fireEvent.change(screen.getByLabelText('Năm học'), { target: { value: '2026-2027' } })
  await waitFor(() => expect(screen.getByText('Chọn yêu cầu chờ trên trang').hasAttribute('disabled')).toBe(false))
  fireEvent.click(screen.getByText('Chọn yêu cầu chờ trên trang'))
  fireEvent.click(screen.getByText('Xử lý 1 yêu cầu đã chọn'))
  fireEvent.change(screen.getByLabelText('Kết quả đối chiếu / lý do'), { target: { value: 'Đã đối chiếu' } })
  fireEvent.click(screen.getByText('Xác nhận'))
  await screen.findByText('Chưa có yêu cầu liên kết.')
  expect(screen.queryByRole('dialog')).toBeNull()
  expect(screen.getByText('Xử lý 0 yêu cầu đã chọn').hasAttribute('disabled')).toBe(true)
  expect(toast.error).toHaveBeenCalledWith('Yêu cầu đã được xử lý.')
})
