// @vitest-environment jsdom
import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { StudentImportForm } from './StudentImportForm'
import { Modal } from '../../components/Modal'
import { useState } from 'react'
import { toast } from 'sonner'

Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })

vi.mock('../../components/ClassPicker', () => ({ ClassPicker: ({ value, onChange, disabled }: { value: string; onChange: (value: string) => void; disabled: boolean }) =>
  <label>Lớp<select value={value} disabled={disabled} onChange={event => onChange(event.target.value)}><option value="">Chọn</option><option value="class-1">A3</option><option value="class-2">A4</option></select></label> }))
afterEach(() => { cleanup(); vi.restoreAllMocks() })
function mount() {
  const done = vi.fn()
  render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><StudentImportForm initialClassId="class-1" onDone={done} onBusy={() => {}} /></QueryClientProvider>)
  const file = new File(['xlsx sample'], 'DanhSach.xlsx')
  fireEvent.change(screen.getByLabelText('File danh sách XLSX'), { target: { files: [file] } })
  return { done, file }
}
it('previews multipart upload and requires confirmation before importing; changing class clears preview', async () => {
  const post = vi.spyOn(api, 'post').mockResolvedValueOnce({ data: { sheetName: 'Sheet1', ignoredColumns: ['SĐT'], canImport: true, rows: [{ row: 4, fullName: 'Nguyễn An', dateOfBirth: '2021-12-03', gender: 'FEMALE', parentPhoneNumber: '0900000001', error: null }, { row: 5, fullName: 'Bình', dateOfBirth: '2021-01-01', gender: 'MALE', parentPhoneNumber: null, error: null }] } })
    .mockResolvedValueOnce({ data: { created: 1 } })
  const { done, file } = mount()
  fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  await screen.findByRole('button', { name: 'Xem chi tiết Nguyễn An' })
  expect(screen.getByRole('dialog', { name: 'Kiểm tra danh sách trẻ' })).toBeTruthy()
  expect(screen.getByText('03/12/2021')).toBeTruthy()
  expect(screen.getByText('Nữ')).toBeTruthy()
  expect(screen.getByText('0900000001')).toBeTruthy()
  expect(screen.getByText('Chưa có')).toBeTruthy()
  const card = screen.getByRole('button', { name: 'Xem chi tiết Nguyễn An' })
  expect(within(card).queryByText('03/12/2021')).toBeNull()
  expect(within(card).queryByText('Nữ')).toBeNull()
  fireEvent.click(card)
  const detail = screen.getByRole('dialog', { name: 'Chi tiết trẻ nhập từ Excel' })
  expect(within(detail).getByText('03/12/2021')).toBeTruthy()
  expect(within(detail).getByText('Nữ')).toBeTruthy()
  fireEvent.click(within(detail).getByText('Quay lại danh sách'))
  const form = post.mock.calls[0][1] as FormData
  expect(form.get('file')).toBe(file)
  expect(form.get('classId')).toBe('class-1')
  expect(screen.getByText('Nhập 2 trẻ').hasAttribute('disabled')).toBe(true)
  fireEvent.click(screen.getByText('Quay lại chọn file'))
  fireEvent.change(screen.getByLabelText('Lớp'), { target: { value: 'class-2' } })
  expect(screen.queryByText('Nguyễn An')).toBeNull()
  // A fresh preview is required after changing the destination class.
  expect(post).toHaveBeenCalledTimes(1)
  expect(done).not.toHaveBeenCalled()
})
it('submits the same file only after the admin accepts a clean preview', async () => {
  const post = vi.spyOn(api, 'post').mockResolvedValueOnce({ data: { sheetName: 'Sheet1', ignoredColumns: [], canImport: true, rows: [{ row: 4, fullName: 'An', error: null }] } })
    .mockResolvedValueOnce({ data: { created: 1 } })
  const { done, file } = mount()
  fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  await screen.findByRole('button', { name: 'Xem chi tiết An' })
  fireEvent.click(screen.getByLabelText('Tôi đã kiểm tra danh sách và lớp nhận trẻ'))
  fireEvent.click(screen.getByText('Nhập 1 trẻ'))
  await waitFor(() => expect(done).toHaveBeenCalledOnce())
  expect(post.mock.calls[1][0]).toBe('/admin/students/import/confirm')
  expect((post.mock.calls[1][1] as FormData).get('file')).toBe(file)
})
it('shows row errors and blocks confirmation without partially importing', async () => {
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { sheetName: 'Sheet1', ignoredColumns: ['SĐT'], canImport: false, rows: [{ row: 4, fullName: 'An', error: 'Trùng họ tên' }] } })
  mount(); fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  await screen.findByText('Trùng họ tên')
  expect(screen.getByText('Nhập 1 trẻ').hasAttribute('disabled')).toBe(true)
  expect(post).toHaveBeenCalledTimes(1)
})

it('returns to the upload dialog and restores scrolling when both dialogs close after import', async () => {
  vi.spyOn(api, 'post').mockResolvedValueOnce({ data: { sheetName: 'Sheet1', ignoredColumns: [], canImport: true, rows: [{ row: 4, fullName: 'An', error: null }] } })
    .mockResolvedValueOnce({ data: { created: 1 } })
  function Harness() {
    const [open, setOpen] = useState(true)
    return open ? <Modal title="Nhập trẻ từ Excel" onClose={() => setOpen(false)}><StudentImportForm initialClassId="class-1" onDone={() => setOpen(false)} onBusy={() => {}} /></Modal> : <p>Đã đóng</p>
  }
  const previousOverflow = document.body.style.overflow
  render(<QueryClientProvider client={new QueryClient()}><Harness /></QueryClientProvider>)
  fireEvent.change(screen.getByLabelText('File danh sách XLSX'), { target: { files: [new File(['sample'], 'test.xlsx')] } })
  fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  await screen.findByRole('dialog', { name: 'Kiểm tra danh sách trẻ' })
  expect(document.body.style.overflow).toBe('hidden')
  fireEvent.click(screen.getByLabelText('Tôi đã kiểm tra danh sách và lớp nhận trẻ'))
  fireEvent.click(screen.getByText('Nhập 1 trẻ'))
  await screen.findByText('Đã đóng')
  expect(screen.queryByRole('dialog')).toBeNull()
  expect(document.body.style.overflow).toBe(previousOverflow)
})

it('shows upload failures as a modal toast without an inline error paragraph', async () => {
  vi.spyOn(api, 'post').mockRejectedValue(new Error('Network failed'))
  const notify = vi.spyOn(toast, 'error')
  mount()
  fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  await waitFor(() => expect(notify).toHaveBeenCalledWith('Có lỗi xảy ra. Vui lòng thử lại.', { toasterId: 'edit-modal' }))
  expect(screen.queryByRole('alert')).toBeNull()
  expect(screen.getByLabelText('File danh sách XLSX')).toBeTruthy()
})

it('shows invalid file feedback as a toast and never sends the file', async () => {
  const post = vi.spyOn(api, 'post')
  const notify = vi.spyOn(toast, 'error')
  mount()
  fireEvent.change(screen.getByLabelText('File danh sách XLSX'), { target: { files: [new File(['text'], 'test.pdf')] } })
  fireEvent.submit(screen.getByLabelText('File danh sách XLSX').closest('form')!)
  expect(notify).toHaveBeenCalledWith('Chọn file XLSX không rỗng, tối đa 5 MB.', { toasterId: 'edit-modal' })
  expect(post).not.toHaveBeenCalled()
})
