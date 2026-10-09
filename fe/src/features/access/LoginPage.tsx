import { useState, type FormEvent } from 'react'
import { ArrowLeft, Heart, Leaf, LockKeyhole, Sparkles } from 'lucide-react'
import { apiErrorMessage } from '../../lib/api'
import type { LoginResponse } from './authApi'
import { login } from './authApi'
import axios from 'axios'
import { RegisterParentForm } from './RegisterParentForm'

const accounts = [
  ['ADMIN', 'admin@demo.mealtrace.local'],
  ['TEACHER', 'teacher@demo.mealtrace.local'],
  ['KITCHEN_STAFF', 'kitchen@demo.mealtrace.local'],
  ['PARENT', 'parent@demo.mealtrace.local'],
] as const

export function LoginPage({ onLogin, onBack }: { onLogin: (result: LoginResponse) => void; onBack: () => void }) {
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

  return <div className="login-layout"><div className="login-side"><button type="button" className="login-back" onClick={onBack}><ArrowLeft size={17} /> Về trang giới thiệu</button><div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>Bữa ăn bán trú</small></span></div><div className="login-side-art" aria-hidden="true"><span><Heart size={53} fill="currentColor" /></span><span>✦</span><span>●</span></div><h1>Mỗi ngày đến lớp,<br />mỗi bữa ăn vui.</h1><p>Một nơi để nhà trường, giáo viên, bếp và phụ huynh cùng chăm sóc bữa ăn của trẻ.</p><div className="login-side-note"><Sparkles size={17} /> Dễ theo dõi. Rõ từng bước. Yên tâm hơn.</div></div><main className="login-main">{registering ? <RegisterParentForm onBack={() => setRegistering(false)} onSuccess={phone => { setEmail(phone); setPassword(''); setError(''); setRegistering(false); setNotice('Đăng ký thành công. Đăng nhập bằng SĐT và mật khẩu vừa tạo.') }} /> : <form className="login-card" onSubmit={submit}><div className="login-icon"><LockKeyhole size={23} /></div><div className="eyebrow">CHÀO MỪNG TRỞ LẠI</div><h2>Đăng nhập</h2><p className="lead">Đăng nhập bằng SĐT hoặc email. Phụ huynh có thể tự đăng ký.</p><label className="field">SĐT hoặc email <input required disabled={pending} type="text" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} /></label><label className="field">Mật khẩu <input required disabled={pending} type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} /></label>{notice && <p role="status">{notice}</p>}{error && <p className="form-error" role="alert">{error}</p>}<button className="button primary login-button" type="submit" disabled={pending}>{pending ? 'Đang xác thực…' : 'Đăng nhập'}</button><button type="button" className="button login-button" disabled={pending} onClick={() => setRegistering(true)}>Đăng ký tài khoản phụ huynh</button>{import.meta.env.DEV && <div className="login-hint"><strong>Tài khoản thử nghiệm</strong><select aria-label="Chọn tài khoản seed" value={email} onChange={event => { setEmail(event.target.value); setPassword('') }}><option value="">Chọn tài khoản</option>{accounts.map(([role, address]) => <option key={role} value={address}>{role} · {address}</option>)}</select><small>Chỉ hiển thị trong môi trường phát triển.</small></div>}</form>}</main></div>
}
