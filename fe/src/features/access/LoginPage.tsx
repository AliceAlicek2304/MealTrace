import { useState, type FormEvent } from 'react'
import { Leaf, LockKeyhole } from 'lucide-react'
import { apiErrorMessage } from '../../lib/api'
import type { LoginResponse } from './authApi'
import { login } from './authApi'
import axios from 'axios'

const accounts = [
  ['ADMIN', 'admin@demo.mealtrace.local'],
  ['TEACHER', 'teacher@demo.mealtrace.local'],
  ['KITCHEN_STAFF', 'kitchen@demo.mealtrace.local'],
  ['NUTRITIONIST', 'nutrition@demo.mealtrace.local'],
  ['ACCOUNTANT', 'accountant@demo.mealtrace.local'],
  ['PARENT', 'parent@demo.mealtrace.local'],
] as const

export function LoginPage({ onLogin }: { onLogin: (result: LoginResponse) => void }) {
  const [email, setEmail] = useState<string>(accounts[0][1])
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setPending(true)
    setError('')
    try { onLogin(await login(email, password)) }
    catch (cause) { setError(axios.isAxiosError(cause) && cause.response?.status === 401 ? 'Email hoặc mật khẩu không đúng, hoặc tài khoản đang bị khóa.' : apiErrorMessage(cause)) }
    finally { setPending(false) }
  }

  return <div className="login-layout"><div className="login-side"><div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>School meal operations</small></span></div><h1>Quản lý bữa ăn<br />có thể truy vết.</h1><p>Đăng nhập bằng tài khoản được phân quyền để tiếp tục công việc theo vai trò.</p><div className="login-side-note">Dữ liệu đăng nhập được xác thực bởi ASP.NET Identity; FE giữ access token trong bộ nhớ phiên.</div></div><main className="login-main"><form className="login-card" onSubmit={submit}><div className="login-icon"><LockKeyhole size={23} /></div><div className="eyebrow">MEALTRACE ACCOUNT</div><h2>Đăng nhập</h2><p className="lead">Chọn tài khoản seed hoặc nhập email tài khoản được Admin tạo.</p><label className="field">Email <input required type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} /></label><label className="field">Mật khẩu <input required type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} /></label>{error && <p className="form-error" role="alert">{error}</p>}<button className="button primary login-button" type="submit" disabled={pending}>{pending ? 'Đang xác thực…' : 'Đăng nhập'}</button><div className="login-hint"><strong>6 tài khoản seed Development</strong><select aria-label="Chọn tài khoản seed" value={email} onChange={event => { setEmail(event.target.value); setPassword('') }}><option value="">Chọn tài khoản</option>{accounts.map(([role, address]) => <option key={role} value={address}>{role} · {address}</option>)}</select><small>Xem mật khẩu trên máy bằng <code>./be/scripts/setup-dev-secrets.ps1 -ShowPasswords</code>. Không gửi mật khẩu trong chat hoặc commit.</small></div></form></main></div>
}
