// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { StudentDirectory } from './StudentDirectory'

let cache: QueryClient
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function(this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function(this: HTMLDialogElement) { this.removeAttribute('open') } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
describe('Class directory search', () => {
  it('debounces class search, uses the API search parameter and distinguishes an empty list from no matches', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], total: 0 } })
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={cache}><StudentDirectory view="classes" initialClassId="" onLink={vi.fn()} onCreate={vi.fn()} onViewStudents={vi.fn()} /></QueryClientProvider>)
    expect(await screen.findByText('Chưa có lớp.')).toBeTruthy()
    fireEvent.change(screen.getByPlaceholderText('Tên lớp hoặc niên khóa'), { target: { value: 'M1' } })
    await waitFor(() => expect(get).toHaveBeenLastCalledWith('/admin/classes', { params: { search: 'M1', page: 1 } }))
    expect(await screen.findByText('Không có lớp phù hợp tìm kiếm.')).toBeTruthy()
  })
})

it('edits birth and gender with the selected profile revision', async () => {
  const student = { id: 'child-1', fullName: 'Nguyễn An', studentCode: 'HS-001', dateOfBirth: '2021-03-04', gender: 'MALE', revision: 7, isActive: true, className: 'A3', parents: [] }
  vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [student], total: 1 } })
  const put = vi.spyOn(api, 'put').mockResolvedValue({ data: {} })
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(<QueryClientProvider client={cache}><StudentDirectory view="students" initialClassId="" onLink={vi.fn()} onCreate={vi.fn()} onViewStudents={vi.fn()} /></QueryClientProvider>)
  fireEvent.click(await screen.findByRole('button', { name: 'Sửa hồ sơ Nguyễn An' }))
  expect((screen.getByLabelText('Ngày sinh') as HTMLInputElement).value).toBe('2021-03-04')
  fireEvent.change(screen.getByLabelText('Ngày sinh'), { target: { value: '2022-04-05' } })
  fireEvent.change(screen.getByLabelText('Giới tính'), { target: { value: 'FEMALE' } })
  fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }))
  await waitFor(() => expect(put).toHaveBeenCalledWith('/admin/students/child-1', { fullName: 'Nguyễn An', updateProfile: true, revision: 7, dateOfBirth: '2022-04-05', gender: 'FEMALE' }))
})
