import { useState, type FormEvent } from 'react'
import { api, apiErrorMessage } from '../../lib/api'
import type { CurrentUser } from './authApi'
import { Modal } from '../../components/Modal'
import { roles } from './model'

export function ProfilePage({ user, onPasswordChanged }: Readonly<{ user: CurrentUser; onPasswordChanged: () => void }>) {
  const [passwordOpen, setPasswordOpen] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)

  async function changePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (newPassword.length < 12) return setError('Mật khẩu mới cần ít nhất 12 ký tự.')
    if (newPassword === currentPassword) return setError('Mật khẩu mới phải khác mật khẩu hiện tại.')
    setPending(true)
    setError('')
    try {
      await api.post('/auth/change-password', { currentPassword, newPassword })
      onPasswordChanged() // Identity revokes the old token; the user signs in again.
    } catch (cause) { setError(apiErrorMessage(cause)) }
    finally { setPending(false) }
  }

  return <><div className="eyebrow">TÀI KHOẢN ĐÃ XÁC THỰC</div><h1>Xin chào, {user.fullName}</h1><p className="lead">Liên hệ: {[user.phoneNumber, user.email].filter(Boolean).join(' · ')}</p><section className="panel profile-panel"><h2>Vai trò của bạn</h2><div className="chips">{user.roles.map(role => <span key={role}>{roles.find(item => item.id === role)?.label ?? role}</span>)}{user.inspectorAccessUntil && <span>Thanh tra đến {user.inspectorAccessUntil}</span>}</div><p>Quyền truy cập dữ liệu được kiểm tra tại API theo vai trò và liên kết lớp/trẻ.</p></section><section className="panel profile-password"><h2>Đổi mật khẩu</h2><div className="workflow-form"><button type="button" className="button primary" onClick={() => { setPasswordOpen(true); setError('') }}>Đổi mật khẩu</button></div></section>{passwordOpen && <Modal title="Đổi mật khẩu" busy={pending} onClose={() => { setPasswordOpen(false); setCurrentPassword(''); setNewPassword(''); setError('') }}><form className="workflow-form" onSubmit={changePassword}><label className="field">Mật khẩu hiện tại<input required type="password" autoComplete="current-password" value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></label><label className="field">Mật khẩu mới<input required type="password" autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} /></label><p className="note">Ít nhất 12 ký tự, có chữ số và ký tự đặc biệt. Bạn sẽ đăng xuất sau khi đổi thành công.</p>{error && <p className="form-error" role="alert">{error}</p>}<button type="submit" className="button primary" disabled={pending}>{pending ? 'Đang đổi…' : 'Đổi mật khẩu'}</button></form></Modal>}</>
}
