// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { ClassesPage } from './ClassesPage'

let cache: QueryClient
beforeEach(() => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })

describe('Teacher registration and notifications', () => {
  it('loads assigned classes without accessing admin scope options', async () => {
    const get = vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url === '/classes' ? [{ id: 'class-a', name: 'M1', schoolYear: '2026-2027' }] : [] }))
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={cache}><ClassesPage isAdmin={false} /></QueryClientProvider>)
    await screen.findByRole('option', { name: 'M1' })
    fireEvent.change(screen.getByLabelText('Lớp'), { target: { value: 'class-a' } })
    await waitFor(() => expect(get).toHaveBeenCalledWith('/classes/class-a/students'))
    expect(get.mock.calls.some(([url]) => url === '/admin/scope-options')).toBe(false)
    expect(screen.queryByRole('button', { name: 'Tạo lớp' })).toBeNull()
  })

  it('preserves account credentials and shows notification failure separately from successful linking', async () => {
    vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url === '/notifications/settings' ? { enabled: true, channel: 'WhatsApp', templateOnly: true } : url === '/classes' ? [{ id: 'class-a', name: 'M1', schoolYear: '2026-2027' }] : [{ id: 'child-a', fullName: 'Trẻ A', classId: 'class-a' }] }))
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { created: true, phoneNumber: '0901234567', temporaryPassword: 'Fake!Password9', notification: { status: 'UNKNOWN', message: 'Hồ sơ đã lưu; chưa rõ kết quả thông báo.', providerMessageId: null } } })
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={cache}><ClassesPage isAdmin={false} /></QueryClientProvider>)
    await screen.findByRole('option', { name: 'M1' })
    fireEvent.change(screen.getByLabelText('Lớp'), { target: { value: 'class-a' } })
    fireEvent.click(await screen.findByRole('button', { name: 'Liên kết phụ huynh' }))
    fireEvent.change(screen.getByLabelText('SĐT phụ huynh'), { target: { value: '0901234567' } })
    fireEvent.change(screen.getByLabelText('Họ tên nếu tạo mới'), { target: { value: 'Parent A' } })
    fireEvent.click(screen.getByRole('checkbox', { name: /Gửi WhatsApp khi liên kết/ }))
    fireEvent.click(screen.getByRole('button', { name: 'Liên kết' }))
    await screen.findByText(/WhatsApp UNKNOWN: Hồ sơ đã lưu/)
    expect(screen.getByText(/WhatsApp trial gửi mẫu cảnh báo số dư demo/)).toBeTruthy()
    expect(screen.getByText('Fake!Password9')).toBeTruthy()
    expect(post).toHaveBeenCalledWith('/admin/students/child-a/parents', { phoneNumber: '0901234567', fullName: 'Parent A', sendRegistrationNotification: true })
  })
})
