// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { LoginPage } from './LoginPage'
import { validateParentRegistration } from './validation'
import * as authApi from './authApi'

afterEach(() => { cleanup(); vi.restoreAllMocks() })
const challenge = { challengeId: 'challenge-1', expiresAt: new Date(Date.now() + 300000).toISOString(), resendAt: new Date(Date.now() + 60000).toISOString(), message: 'OTP đã gửi.' }
async function receiveOtp() {
  vi.spyOn(authApi, 'requestParentOtp').mockResolvedValue(challenge)
  fireEvent.click(screen.getByText('Gửi OTP qua WhatsApp'))
  fireEvent.change(await screen.findByLabelText('Mã OTP WhatsApp'), { target: { value: '123456' } })
}
const draft = { fullName: 'Phụ huynh', phoneNumber: '+84 901234567', password: 'ParentSignup!123' }
function fill() {
  fireEvent.change(screen.getByLabelText('Họ tên'), { target: { value: draft.fullName } })
  fireEvent.change(screen.getByLabelText('Số điện thoại'), { target: { value: draft.phoneNumber } })
  fireEvent.change(screen.getByLabelText(/^Mật khẩu/), { target: { value: draft.password } })
  fireEvent.change(screen.getByLabelText('Nhập lại mật khẩu'), { target: { value: draft.password } })
}
describe('Parent signup', () => {
  it('requires a fresh OTP after editing the phone and sends no password when requesting OTP', async () => {
    const register = vi.spyOn(authApi, 'registerParent')
    render(<LoginPage onLogin={vi.fn()} onBack={vi.fn()} />)
    fireEvent.click(screen.getByText('Đăng ký tài khoản phụ huynh')); fill(); await receiveOtp()
    expect(authApi.requestParentOtp).toHaveBeenCalledExactlyOnceWith('0901234567')
    expect(screen.getByText(/Gửi lại sau/).hasAttribute('disabled')).toBe(true)
    fireEvent.change(screen.getByLabelText('Số điện thoại'), { target: { value: '0905555555' } })
    expect(screen.queryByLabelText('Mã OTP WhatsApp')).toBeNull()
    fireEvent.click(screen.getByText('Tạo tài khoản phụ huynh'))
    expect(screen.getByRole('alert').textContent).toContain('OTP')
    expect(register).not.toHaveBeenCalled()
  })
  it('validates phone, password strength and confirmation', () => {
    expect(validateParentRegistration(draft, draft.password)).toBeNull()
    expect(validateParentRegistration({ ...draft, phoneNumber: '123' }, draft.password)).toContain('SĐT')
    expect(validateParentRegistration({ ...draft, password: 'lowercase123!' }, 'lowercase123!')).toContain('Mật khẩu')
    expect(validateParentRegistration(draft, 'other')).toContain('chưa khớp')
  })
  it('submits only parent fields once and returns to login without signing in', async () => {
    let finish!: () => void
    const request = vi.spyOn(authApi, 'registerParent').mockImplementation(() => new Promise<void>(resolve => { finish = resolve }))
    const onLogin = vi.fn()
    render(<LoginPage onLogin={onLogin} onBack={vi.fn()} />)
    fireEvent.click(screen.getByText('Đăng ký tài khoản phụ huynh')); fill(); await receiveOtp()
    expect(screen.queryByLabelText('Chọn tài khoản seed')).toBeNull()
    fireEvent.click(screen.getByText('Tạo tài khoản phụ huynh'))
    expect(screen.getByText('Đang đăng ký…').hasAttribute('disabled')).toBe(true)
    expect(request).toHaveBeenCalledExactlyOnceWith({ ...draft, phoneNumber: '0901234567', challengeId: challenge.challengeId, otpCode: '123456' })
    finish()
    await waitFor(() => expect(screen.getByRole('status').textContent).toContain('Đăng ký thành công'))
    expect((screen.getByLabelText('SĐT hoặc email') as HTMLInputElement).value).toBe('0901234567')
    expect(onLogin).not.toHaveBeenCalled()
  })
  it('keeps signup open on a duplicate phone response', async () => {
    vi.spyOn(authApi, 'registerParent').mockRejectedValue({ isAxiosError: true, response: { status: 409, data: { message: 'SĐT đã có tài khoản.' } } })
    render(<LoginPage onLogin={vi.fn()} onBack={vi.fn()} />)
    fireEvent.click(screen.getByText('Đăng ký tài khoản phụ huynh')); fill(); await receiveOtp()
    fireEvent.click(screen.getByText('Tạo tài khoản phụ huynh'))
    expect((await screen.findByRole('alert')).textContent).toBe('SĐT đã có tài khoản.')
    expect((screen.getByLabelText('Số điện thoại') as HTMLInputElement).value).toBe(draft.phoneNumber)
  })
})
