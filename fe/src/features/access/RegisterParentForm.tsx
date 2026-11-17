import { useEffect, useRef, useState, type FormEvent } from 'react'
import { registerParent, requestParentOtp, type ParentOtpChallenge, type ParentRegistration } from './authApi'
import { normalizePhone, validateParentRegistration } from './validation'
import { apiErrorMessage } from '../../lib/api'

export function RegisterParentForm({ onSuccess, onBack }: Readonly<{ onSuccess: (phone: string) => void; onBack: () => void }>) {
  const [draft, setDraft] = useState<ParentRegistration>({ fullName: '', phoneNumber: '', password: '' })
  const [confirmation, setConfirmation] = useState('')
  const [pending, setPending] = useState(false)
  const [sendingOtp, setSendingOtp] = useState(false)
  const [showPassword, setShowPassword] = useState(false)
  const otpRef = useRef<HTMLInputElement>(null)
  const [error, setError] = useState('')
  const [challenge, setChallenge] = useState<ParentOtpChallenge | null>(null)
  const [otpCode, setOtpCode] = useState('')
  const [resendAt, setResendAt] = useState(0)
  const [now, setNow] = useState(Date.now())
  useEffect(() => {
    const endsAt = Math.max(resendAt, Date.parse(challenge?.expiresAt ?? '') || 0)
    if (Date.now() >= endsAt) return
    const timer = window.setInterval(() => {
      const current = Date.now()
      setNow(current)
      if (current >= endsAt) window.clearInterval(timer)
    }, 1000)
    return () => window.clearInterval(timer)
  }, [resendAt, challenge])
  useEffect(() => { if (challenge) otpRef.current?.focus() }, [challenge])
  const resendSeconds = Math.max(0, Math.ceil((resendAt - now) / 1000))
  let otpButtonLabel = 'Gửi OTP qua WhatsApp'
  if (challenge) otpButtonLabel = 'Gửi lại OTP WhatsApp'
  if (resendSeconds > 0) otpButtonLabel = `Gửi lại sau ${resendSeconds}s`
  if (sendingOtp) otpButtonLabel = 'Đang gửi OTP…'
  async function sendOtp() {
    if (pending || resendSeconds > 0) return
    const phone = normalizePhone(draft.phoneNumber)
    if (!phone) { setError('SĐT Việt Nam không hợp lệ.'); return }
    const startedAt = Date.now()
    setPending(true); setSendingOtp(true); setError(''); setNow(startedAt); setResendAt(startedAt + 60000)
    try {
      setChallenge(null)
      const received = await requestParentOtp(phone)
      setChallenge(received); setResendAt(Date.parse(received.resendAt)); setOtpCode(''); setNow(Date.now())
    }
    catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setPending(false); setSendingOtp(false) }
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
    <p className="lead">Đăng ký xong, đăng nhập và vào Liên kết trẻ để chọn năm học, lớp và gửi yêu cầu. Nhà trường duyệt xong, bạn có thể xem và báo vắng cho con.</p>
    <label className="field">Họ tên<input required maxLength={120} autoComplete="name" disabled={pending} value={draft.fullName} onChange={e => setDraft({ ...draft, fullName: e.target.value })} /></label>
    <label className="field">Số điện thoại<input required type="tel" maxLength={30} autoComplete="tel" disabled={pending} value={draft.phoneNumber} onChange={e => { setDraft({ ...draft, phoneNumber: e.target.value }); setChallenge(null); setOtpCode(''); setResendAt(0) }} /></label>
    <button type="button" className="button login-button" disabled={pending || resendSeconds > 0} onClick={sendOtp}>{otpButtonLabel}</button>
    {challenge && <><output aria-live="polite">{challenge.message}</output><label className="field">Mã OTP WhatsApp<input ref={otpRef} inputMode="numeric" autoComplete="one-time-code" maxLength={6} disabled={pending} value={otpCode} onChange={e => setOtpCode(e.target.value.replace(/\D/g, ''))} /></label><small>{Date.parse(challenge.expiresAt) <= now ? 'Mã đã hết hạn. Vui lòng gửi lại OTP.' : 'Nhập mã trong 5 phút. Gửi lại sẽ vô hiệu mã cũ.'}</small></>}
    <label className="field">Mật khẩu<input required type={showPassword ? 'text' : 'password'} maxLength={128} autoComplete="new-password" disabled={pending} value={draft.password} onChange={e => setDraft({ ...draft, password: e.target.value })} /><small>Ít nhất 12 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.</small></label>
    <label className="field">Nhập lại mật khẩu<input required type={showPassword ? 'text' : 'password'} maxLength={128} autoComplete="new-password" disabled={pending} value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>
    <label className="switch-line password-visibility"><input type="checkbox" disabled={pending} checked={showPassword} onChange={event => setShowPassword(event.target.checked)} />Hiện mật khẩu</label>
    {error && <p role="alert" className="form-error">{error}</p>}
    <button type="submit" className="button primary login-button" disabled={pending}>{pending && !sendingOtp ? 'Đang đăng ký…' : 'Tạo tài khoản phụ huynh'}</button>
    <button type="button" className="button login-button" disabled={pending} onClick={onBack}>Quay lại đăng nhập</button>
  </form>
}
