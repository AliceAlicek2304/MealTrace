import { useState, type FormEvent } from 'react'
import { emptyDraft, roles, type Role, type SchoolUser, type UserDraft } from './model'
import { validateUserDraft } from './validation'
import { ScopePicker } from './ScopePicker'
import { toast } from 'sonner'

type Props = Readonly<{
  current: SchoolUser | null
  users: SchoolUser[]
  onSave: (draft: UserDraft) => Promise<void>
  onCancel: () => void
  onSavingChange?: (saving: boolean) => void
}>

const toggle = <T,>(items: T[], item: T): T[] => items.includes(item) ? items.filter(value => value !== item) : [...items, item]

export function UserForm({ current, users, onSave, onCancel, onSavingChange }: Props) {
  const [draft, setDraft] = useState<UserDraft>(() => current ? {
    fullName: current.fullName, email: current.email, phoneNumber: current.phoneNumber ?? '', roles: [...current.roles], status: current.status,
    classIds: [...current.classIds], studentIds: [...current.studentIds], inspectorAccessUntil: current.inspectorAccessUntil,
  } : emptyDraft())
  const [saving, setSaving] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const normalized: UserDraft = { ...draft, fullName: draft.fullName.trim(), email: draft.email.trim().toLowerCase() }
    const validationError = validateUserDraft(normalized, users, current?.id ?? null)
    if (validationError) { toast.error(validationError, { toasterId: 'edit-modal' }); return }
    setSaving(true); onSavingChange?.(true)
    try { await onSave(normalized) }
    catch (cause) { toast.error(cause instanceof Error ? cause.message : 'Không lưu được tài khoản.', { toasterId: 'edit-modal' }) }
    finally { setSaving(false); onSavingChange?.(false) }
  }
  let submitLabel = 'Tạo tài khoản'
  if (saving) submitLabel = 'Đang lưu…'
  else if (current) submitLabel = 'Lưu thay đổi'

  return <form className="user-form" onSubmit={submit}>
    <div className="form-grid">
      <label className="field">Họ và tên <input required maxLength={120} value={draft.fullName} onChange={event => setDraft({ ...draft, fullName: event.target.value })} placeholder="Nguyễn Văn A" /></label>
      <label className="field">Email (không bắt buộc nếu có SĐT) <input type="email" maxLength={254} value={draft.email} onChange={event => setDraft({ ...draft, email: event.target.value })} placeholder="name@school.edu.vn" /></label>
      <label className="field">Số điện thoại đăng nhập <input type="tel" maxLength={30} value={draft.phoneNumber ?? ''} onChange={event => setDraft({ ...draft, phoneNumber: event.target.value })} placeholder="0901234567" /></label>
      <label className="field">Trạng thái <select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value as UserDraft['status'] })}><option value="ACTIVE">Đang hoạt động</option><option value="SUSPENDED">Tạm khóa</option></select></label>
    </div>

    <fieldset className="form-section role-fieldset"><legend>Vai trò nghiệp vụ</legend><p>Một tài khoản có thể mang nhiều vai trò.</p><div className="role-choices">{roles.map(role => <label className="choice" key={role.id}><input type="checkbox" checked={draft.roles.includes(role.id)} onChange={() => setDraft({ ...draft, roles: toggle<Role>(draft.roles, role.id) })} /><span><strong>{role.label}</strong><small>{role.description}</small></span></label>)}</div></fieldset>

    {draft.roles.includes('TEACHER') && <div className="form-section"><h3>Phạm vi giáo viên</h3><p>Chỉ giao những lớp giáo viên được phụ trách.</p><ScopePicker kind="classes" selected={draft.classIds} onChange={classIds => setDraft({ ...draft, classIds })} /></div>}
    {draft.roles.includes('PARENT') && <div className="form-section"><h3>Liên kết phụ huynh – học sinh</h3><p>Có thể để trống và liên kết trẻ sau. Phụ huynh chỉ được xem dữ liệu học sinh đã liên kết. Có thể ghép nhanh tại màn Lớp và trẻ bằng SĐT phụ huynh.</p><ScopePicker kind="students" selected={draft.studentIds} onChange={studentIds => setDraft({ ...draft, studentIds })} /></div>}

    <div className="form-section"><h3>Quyền thanh tra tạm thời</h3><p>Quyền chỉ đọc, tự hết hiệu lực theo ngày; tách khỏi 4 vai trò nghiệp vụ.</p><label className="switch-line"><input type="checkbox" checked={draft.inspectorAccessUntil !== null} onChange={event => setDraft({ ...draft, inspectorAccessUntil: event.target.checked ? '' : null })} /> Cấp quyền đọc cho thanh tra</label>{draft.inspectorAccessUntil !== null && <label className="field expiry-field">Ngày hết hạn <input required type="date" value={draft.inspectorAccessUntil} onChange={event => setDraft({ ...draft, inspectorAccessUntil: event.target.value })} /></label>}</div>

    <div className="form-actions"><button type="button" className="button secondary" onClick={onCancel} disabled={saving}>Hủy</button><button type="submit" className="button primary" disabled={saving}>{submitLabel}</button></div>
  </form>
}
