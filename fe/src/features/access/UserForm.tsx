import { useState, type FormEvent } from 'react'
import { emptyDraft, roles, type Role, type SchoolUser, type UserDraft } from './model'
import { validateUserDraft } from './validation'
import type { ScopeOptions } from './authApi'

type Props = {
  current: SchoolUser | null
  users: SchoolUser[]
  scopes: ScopeOptions
  onSave: (draft: UserDraft) => Promise<void>
  onCancel: () => void
}

const toggle = <T,>(items: T[], item: T): T[] => items.includes(item) ? items.filter(value => value !== item) : [...items, item]

export function UserForm({ current, users, scopes, onSave, onCancel }: Props) {
  const [draft, setDraft] = useState<UserDraft>(() => current ? {
    fullName: current.fullName, email: current.email, roles: [...current.roles], status: current.status,
    classIds: [...current.classIds], studentIds: [...current.studentIds], inspectorAccessUntil: current.inspectorAccessUntil,
  } : emptyDraft())
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalized: UserDraft = { ...draft, fullName: draft.fullName.trim(), email: draft.email.trim().toLowerCase() }
    const validationError = validateUserDraft(normalized, users, current?.id ?? null)
    if (validationError) return setError(validationError)
    setSaving(true)
    setError('')
    try { await onSave(normalized) }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Không lưu được tài khoản.') }
    finally { setSaving(false) }
  }

  return <form className="user-form" onSubmit={submit}>
    <div className="form-grid">
      <label className="field">Họ và tên <input required maxLength={120} value={draft.fullName} onChange={event => setDraft({ ...draft, fullName: event.target.value })} placeholder="Nguyễn Văn A" /></label>
      <label className="field">Email đăng nhập <input required type="email" maxLength={254} value={draft.email} onChange={event => setDraft({ ...draft, email: event.target.value })} placeholder="name@school.edu.vn" /></label>
      <label className="field">Trạng thái <select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value as UserDraft['status'] })}><option value="ACTIVE">Đang hoạt động</option><option value="SUSPENDED">Tạm khóa</option></select></label>
    </div>

    <div className="form-section"><h3>Vai trò nghiệp vụ</h3><p>Một tài khoản có thể mang nhiều vai trò.</p><div className="role-choices">{roles.map(role => <label className="choice" key={role.id}><input type="checkbox" checked={draft.roles.includes(role.id)} onChange={() => setDraft({ ...draft, roles: toggle<Role>(draft.roles, role.id) })} /><span><strong>{role.label}</strong><small>{role.description}</small></span></label>)}</div></div>

    {draft.roles.includes('TEACHER') && <div className="form-section"><h3>Phạm vi giáo viên · ET-06</h3><p>Chỉ giao những lớp giáo viên được phụ trách.</p><div className="inline-choices">{scopes.classes.map(item => <label key={item.id}><input type="checkbox" checked={draft.classIds.includes(item.id)} onChange={() => setDraft({ ...draft, classIds: toggle(draft.classIds, item.id) })} /> {item.name}</label>)}</div></div>}
    {draft.roles.includes('PARENT') && <div className="form-section"><h3>Liên kết phụ huynh – học sinh · ET-07</h3><p>Phụ huynh chỉ được xem dữ liệu học sinh đã liên kết.</p><div className="inline-choices">{scopes.students.map(item => <label key={item.id}><input type="checkbox" checked={draft.studentIds.includes(item.id)} onChange={() => setDraft({ ...draft, studentIds: toggle(draft.studentIds, item.id) })} /> {item.name} <small>({item.id})</small></label>)}</div></div>}

    <div className="form-section"><h3>Quyền thanh tra tạm thời · FR-07</h3><p>Quyền chỉ đọc, tự hết hiệu lực theo ngày; tách khỏi 6 vai trò nghiệp vụ.</p><label className="switch-line"><input type="checkbox" checked={draft.inspectorAccessUntil !== null} onChange={event => setDraft({ ...draft, inspectorAccessUntil: event.target.checked ? '' : null })} /> Cấp quyền đọc cho thanh tra</label>{draft.inspectorAccessUntil !== null && <label className="field expiry-field">Ngày hết hạn <input required type="date" value={draft.inspectorAccessUntil} onChange={event => setDraft({ ...draft, inspectorAccessUntil: event.target.value })} /></label>}</div>

    {error && <p className="form-error" role="alert">{error}</p>}
    <div className="form-actions"><button type="button" className="button secondary" onClick={onCancel} disabled={saving}>Hủy</button><button type="submit" className="button primary" disabled={saving}>{saving ? 'Đang lưu…' : current ? 'Lưu thay đổi' : 'Tạo tài khoản'}</button></div>
  </form>
}
