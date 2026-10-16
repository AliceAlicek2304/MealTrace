import { useState, type FormEvent } from 'react'
import { Heart, Leaf, LockKeyhole, Sparkles } from 'lucide-react'
import { apiErrorMessage } from '../../lib/api'
import type { LoginResponse } from './authApi'
import { login } from './authApi'
import axios from 'axios'
import { RegisterParentForm } from './RegisterParentForm'

const accounts = [
  ['Quản lý nhà trường', 'admin@demo.mealtrace.local'],
  ['Giáo viên', 'teacher@demo.mealtrace.local'],
  ['Nhân viên bếp', 'kitchen@demo.mealtrace.local'],
  ['Phụ huynh', 'parent@demo.mealtrace.local'],
] as const

export function LoginPage({ onLogin }: Readonly<{ onLogin: (result: LoginResponse) => void }>) {
  const [email, setEmail] = useState<string>(import.meta.env.DEV ? accounts[0][1] : '')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [registering, setRegistering] = useState(false)
  const [notice, setNotice] = useState('')

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (pending) return
    setPending(true)
    setError('')
    try { onLogin(await login(email, password)) }
    catch (cause) { setError(axios.isAxiosError(cause) && cause.response?.status === 401 ? 'SĐT/email hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.' : apiErrorMessage(cause)) }
    finally { setPending(false) }
  }

  const registrationSuccess = (phone: string) => {
    setEmail(phone); setPassword(''); setError(''); setRegistering(false)
    setNotice('Đăng ký thành công. Đăng nhập bằng SĐT và mật khẩu vừa tạo.')
  }
  const loginNotice = notice ? <output aria-live="polite">{notice}</output> : null
  const loginError = error ? <p className="form-error" role="alert">{error}</p> : null
  return <div className="login-layout"><div className="login-side"><div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>Quản lý bữa ăn bán trú</small></span></div><div className="login-side-art" aria-hidden="true"><span><Heart size={53} fill="currentColor" /></span><span>✦</span><span>●</span></div><h1>Quản lý bữa ăn<br />bán trú.</h1><p>Hỗ trợ cán bộ quản lý và giáo viên theo dõi lịch ăn, danh sách lớp, số suất và thông tin điều chỉnh.</p><div className="login-side-note"><Sparkles size={17} /> Phân quyền theo vai trò · Lưu lịch sử xử lý</div></div><main className="login-main">{registering ? <RegisterParentForm onBack={() => setRegistering(false)} onSuccess={registrationSuccess} /> : <form className="login-card" onSubmit={submit}><div className="login-icon"><LockKeyhole size={23} /></div><div className="eyebrow">TRUY CẬP HỆ THỐNG</div><h2>Đăng nhập</h2><p className="lead">Nhập thông tin tài khoản do nhà trường cấp. Phụ huynh chưa có tài khoản có thể đăng ký.</p><label className="field">SĐT hoặc email <input required disabled={pending} type="text" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} /></label><label className="field">Mật khẩu <input required disabled={pending} type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} /></label>{loginNotice}{loginError}<button className="button primary login-button" type="submit" disabled={pending}>{pending ? 'Đang xác thực…' : 'Đăng nhập'}</button><button type="button" className="button login-button" disabled={pending} onClick={() => setRegistering(true)}>Đăng ký phụ huynh</button>{import.meta.env.DEV && <div className="login-hint"><strong>Tài khoản thử nghiệm</strong><select aria-label="Chọn tài khoản thử nghiệm" value={email} onChange={event => { setEmail(event.target.value); setPassword('') }}><option value="">Chọn tài khoản</option>{accounts.map(([role, address]) => <option key={role} value={address}>{role} · {address}</option>)}</select><small>Chỉ hiển thị trong môi trường phát triển.</small></div>}</form>}</main></div>
}
