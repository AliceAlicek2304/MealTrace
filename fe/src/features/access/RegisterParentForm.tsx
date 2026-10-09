import { useEffect, useState, type FormEvent } from 'react'
import { registerParent, requestParentOtp, type ParentOtpChallenge, type ParentRegistration } from './authApi'
import { normalizePhone, validateParentRegistration } from './validation'
import { apiErrorMessage } from '../../lib/api'

export function RegisterParentForm({ onSuccess, onBack }: Readonly<{ onSuccess: (phone: string) => void; onBack: () => void }>) {
  const [draft, setDraft] = useState<ParentRegistration>({ fullName: '', phoneNumber: '', password: '' })
  const [confirmation, setConfirmation] = useState('')
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [challenge, setChallenge] = useState<ParentOtpChallenge | null>(null)
  const [otpCode, setOtpCode] = useState('')
  const [resendAt, setResendAt] = useState(0)
  const [now, setNow] = useState(Date.now())
  useEffect(() => { const timer = window.setInterval(() => setNow(Date.now()), 1000); return () => window.clearInterval(timer) }, [])
  const resendSeconds = Math.max(0, Math.ceil((resendAt - now) / 1000))
  let otpButtonLabel = 'Gửi OTP qua WhatsApp'
  if (challenge) otpButtonLabel = 'Gửi lại OTP WhatsApp'
  if (resendSeconds > 0) otpButtonLabel = `Gửi lại sau ${resendSeconds}s`
  async function sendOtp() {
    if (pending || resendSeconds > 0) return
    const phone = normalizePhone(draft.phoneNumber)
    if (!phone) { setError('SĐT Việt Nam không hợp lệ.'); return }
    setPending(true); setError(''); setResendAt(Date.now() + 60000)
    try { setChallenge(null); setChallenge(await requestParentOtp(phone)); setOtpCode(''); setNow(Date.now()) }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setPending(false) }
  }
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (pending) return
    const invalid = validateParentRegistration(draft, confirmation)
    if (invalid) { setError(invalid); return }
    if (!challenge || !/^\d{6}$/.test(otpCode)) { setError('Vui lòng nhận và nhập 6 chữ số OTP từ WhatsApp.'); return }
    if (Date.parse(challenge.expiresAt) <= Date.now()) { setError('OTP đã hết hạn. Vui lòng gửi lại mã.'); return }
    setPending(true); setError('')
    try {
      const phone = normalizePhone(draft.phoneNumber)!
      await registerParent({ ...draft, phoneNumber: phone, challengeId: challenge.challengeId, otpCode })
      onSuccess(phone)
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setPending(false) }
  }
  return <form className="login-card" onSubmit={submit}>
    <div className="eyebrow">DÀNH CHO PHỤ HUYNH</div><h2>Đăng ký tài khoản</h2>
    <p className="lead">Sau khi đăng ký, nhà trường cần liên kết tài khoản với trẻ để bạn xem dữ liệu.</p>
    <label className="field">Họ tên<input required maxLength={120} autoComplete="name" disabled={pending} value={draft.fullName} onChange={e => setDraft({ ...draft, fullName: e.target.value })} /></label>
    <label className="field">Số điện thoại<input required type="tel" maxLength={30} autoComplete="tel" disabled={pending} value={draft.phoneNumber} onChange={e => { setDraft({ ...draft, phoneNumber: e.target.value }); setChallenge(null); setOtpCode(''); setResendAt(0) }} /></label>
    <button type="button" className="button login-button" disabled={pending || resendSeconds > 0} onClick={sendOtp}>{otpButtonLabel}</button>
    {challenge && <><output aria-live="polite">{challenge.message}</output><label className="field">Mã OTP WhatsApp<input inputMode="numeric" autoComplete="one-time-code" maxLength={6} disabled={pending} value={otpCode} onChange={e => setOtpCode(e.target.value.replace(/\D/g, ''))} /></label><small>Nhập mã trong 5 phút. Gửi lại sẽ vô hiệu mã cũ.</small></>}
    <label className="field">Mật khẩu<input required type="password" maxLength={128} autoComplete="new-password" disabled={pending} value={draft.password} onChange={e => setDraft({ ...draft, password: e.target.value })} /><small>Ít nhất 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.</small></label>
    <label className="field">Nhập lại mật khẩu<input required type="password" maxLength={128} autoComplete="new-password" disabled={pending} value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>
    {error && <p role="alert" className="form-error">{error}</p>}
    <button type="submit" className="button primary login-button" disabled={pending}>{pending ? 'Đang đăng ký…' : 'Tạo tài khoản phụ huynh'}</button>
    <button type="button" className="button login-button" disabled={pending} onClick={onBack}>Quay lại đăng nhập</button>
  </form>
}
