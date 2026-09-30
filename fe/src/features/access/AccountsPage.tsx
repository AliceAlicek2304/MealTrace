import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { KeyRound, Pencil, Plus, Search, ShieldCheck, Users } from 'lucide-react'
import { apiErrorMessage } from '../../lib/api'
import { createUser, getScopeOptions, listUsers, resetUserPassword, updateUser } from './authApi'
import { roles, type Role, type UserDraft, type SchoolUser } from './model'
import { UserForm } from './UserForm'
import { Modal } from '../../components/Modal'
import { toast } from 'sonner'

const roleName = (id: Role) => roles.find(role => role.id === id)?.label ?? id

export function AccountsPage() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<Role | 'ALL'>('ALL')
  const [classFilter, setClassFilter] = useState('')
  const [current, setCurrent] = useState<SchoolUser | null>(null)
  const [editSaving, setEditSaving] = useState(false)
  const [formOpen, setFormOpen] = useState(false)
  const [resetTarget, setResetTarget] = useState<SchoolUser | null>(null)
  const [resetReason, setResetReason] = useState('')
  const [resetPending, setResetPending] = useState(false)
  const [credentialLabel, setCredentialLabel] = useState('')
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const userQuery = useQuery({ queryKey: ['admin-users', page, classFilter], queryFn: () => listUsers(page, classFilter) })
  const scopeQuery = useQuery({ queryKey: ['scope-options'], queryFn: getScopeOptions })
  const users = userQuery.data?.items ?? []
  const scopes = scopeQuery.data ?? { classes: [], students: [] }
  const filtered = useMemo(() => users.filter(user => {
    const matchesText = `${user.fullName} ${user.email} ${user.phoneNumber ?? ''}`.toLocaleLowerCase('vi-VN').includes(search.toLocaleLowerCase('vi-VN').trim())
    return matchesText && (roleFilter === 'ALL' || user.roles.includes(roleFilter))
  }), [users, search, roleFilter])

  async function save(draft: UserDraft) {
    const cleaned = {
      ...draft,
      classIds: draft.roles.includes('TEACHER') ? draft.classIds : [],
      studentIds: draft.roles.includes('PARENT') ? draft.studentIds : [],
    }
    try {
      if (current) {
        await updateUser(current.id, cleaned)
        setTemporaryPassword('')
        toast.success('Đã cập nhật tài khoản.')
      } else {
        const result = await createUser(cleaned)
        setTemporaryPassword(result.temporaryPassword)
        setCredentialLabel(`${result.user.fullName} · ${result.user.phoneNumber || result.user.email}`)
        toast.success(`Đã tạo ${result.user.phoneNumber || result.user.email}. Mật khẩu tạm hiển thị bên dưới.`)
        setPage(1)
      }
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      setFormOpen(false)
      setCurrent(null)
    } catch (error) { throw new Error(apiErrorMessage(error)) }
  }

  async function confirmReset() {
    if (!resetTarget || resetPending || !resetReason.trim()) return
    setResetPending(true)
    setTemporaryPassword('')
    try {
      const result = await resetUserPassword(resetTarget.id, resetReason.trim())
      setCredentialLabel(`${result.fullName} · ${result.phoneNumber || result.email || ''}`)
      setTemporaryPassword(result.temporaryPassword)
      setResetTarget(null); setResetReason('')
      toast.success(result.isActive ? 'Đã đặt lại mật khẩu và thu hồi phiên cũ.' : 'Đã đặt lại mật khẩu. Tài khoản vẫn đang tạm khóa.')
    } catch (error) { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) }
    finally { setResetPending(false) }
  }

  const total = userQuery.data?.total ?? 0
  return <><div className="page-head"><div><div className="eyebrow">FR-02 · ET-05 / ET-06 / ET-07</div><h1>Quản lý tài khoản</h1><p className="lead">Gán nhiều vai trò và giới hạn dữ liệu theo lớp, học sinh hoặc quyền thanh tra có thời hạn.</p></div><button type="button" className="button primary" onClick={() => { setResetTarget(null); setCurrent(null); setFormOpen(true); setTemporaryPassword('') }}><Plus size={17} /> Thêm tài khoản</button></div>
    <div className="demo-banner"><ShieldCheck size={20} /><div><strong>Dữ liệu tài khoản từ PostgreSQL</strong><span>Chỉ Admin truy cập API này. Mật khẩu tạm của tài khoản mới được hiển thị một lần và không lưu ở FE.</span></div></div>
    <div className="stats"><div><span>Tổng tài khoản</span><strong>{userQuery.isPending ? '—' : total}</strong></div><div><span>Đang hoạt động trong trang</span><strong>{userQuery.isPending ? '—' : users.filter(user => user.status === 'ACTIVE').length}</strong></div><div><span>Đa vai trò trong trang</span><strong>{userQuery.isPending ? '—' : users.filter(user => user.roles.length > 1).length}</strong></div></div>
    {temporaryPassword && <Modal title="Thông tin đăng nhập" description={credentialLabel} onClose={() => setTemporaryPassword('')}><div className="credential-once"><strong>{credentialLabel} · Mật khẩu tạm:</strong> <code>{temporaryPassword}</code><button type="button" onClick={() => setTemporaryPassword('')}>Đã lưu, ẩn mật khẩu</button><small>Hãy chuyển riêng cho người dùng qua kênh an toàn. Tải lại trang sẽ không xem lại được.</small></div></Modal>}
    {resetTarget && <Modal title={`Đặt lại mật khẩu: ${resetTarget.fullName}`} description={resetTarget.phoneNumber || resetTarget.email} busy={resetPending} onClose={() => { setResetTarget(null); setResetReason('') }}>
      <form className="workflow-form" onSubmit={event => { event.preventDefault(); void confirmReset() }}>
        <p className="password-reset-note">Mật khẩu cũ và các phiên đăng nhập cũ sẽ mất hiệu lực. Nhà trường đối chiếu hồ sơ người dùng trước khi cấp lại.</p>
        <label className="field">Lý do<input required maxLength={500} value={resetReason} disabled={resetPending} onChange={event => setResetReason(event.target.value)} placeholder="Phụ huynh quên mật khẩu, đã đối chiếu hồ sơ" /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={resetPending} onClick={() => { setResetTarget(null); setResetReason('') }}>Hủy</button><button type="submit" className="button primary" disabled={resetPending || !resetReason.trim()}>{resetPending ? 'Đang đặt lại…' : 'Cấp mật khẩu tạm mới'}</button></div>
      </form></Modal>}
    {formOpen && <Modal title={current ? `Chỉnh sửa ${current.fullName}` : 'Thêm tài khoản mới'} description="Vai trò và phạm vi được kiểm tra trước khi lưu." wide busy={editSaving} onClose={() => { setFormOpen(false); setCurrent(null) }}>
      {scopeQuery.isPending ? <div className="empty compact">Đang tải lớp và học sinh…</div> : scopeQuery.isError ? <div className="empty compact error">Không tải được danh mục phạm vi.</div> : <UserForm key={current?.id ?? 'new'} current={current} users={users} scopes={scopes} onSave={save} onSavingChange={setEditSaving} onCancel={() => { setFormOpen(false); setCurrent(null) }} />}
    </Modal>}
    <section className="panel"><div className="panel-head"><div><h2>Danh sách tài khoản</h2><p>{total} tài khoản · trang {page}</p></div><Users size={20} /></div><div className="toolbar"><label className="search"><Search size={17} /><input aria-label="Tìm tài khoản trong trang" placeholder="Tìm trong trang hiện tại" value={search} onChange={event => setSearch(event.target.value)} /></label><select aria-label="Lọc theo vai trò trong trang" value={roleFilter} onChange={event => setRoleFilter(event.target.value as Role | 'ALL')}><option value="ALL">Tất cả vai trò</option>{roles.map(role => <option key={role.id} value={role.id}>{role.label}</option>)}</select><select aria-label="Lọc giáo viên theo lớp phụ trách" value={classFilter} onChange={event => { setClassFilter(event.target.value); setPage(1) }}><option value="">Tất cả lớp phụ trách</option>{scopes.classes.map(room => <option key={room.id} value={room.id}>{room.name}</option>)}</select></div>
      {userQuery.isPending ? <div className="empty compact">Đang tải tài khoản…</div> : userQuery.isError ? <div className="empty compact error">{apiErrorMessage(userQuery.error)}</div> : <div className="table-wrap"><table><thead><tr><th>Tài khoản</th><th>Vai trò</th><th>Phạm vi</th><th>Trạng thái</th><th></th></tr></thead><tbody>{filtered.map(user => <tr key={user.id}><td><strong>{user.fullName}</strong><small>{[user.phoneNumber, user.email].filter(Boolean).join(' · ')}</small></td><td><div className="chips">{user.roles.map(role => <span key={role}>{roleName(role)}</span>)}{user.inspectorAccessUntil && <span className="inspector-chip">Thanh tra đến {user.inspectorAccessUntil}</span>}</div></td><td className="scope-cell">{user.roles.includes('TEACHER') && <span>Lớp: {user.classIds.map(id => scopes.classes.find(item => item.id === id)?.name ?? id).join(', ') || 'Chưa gán'}</span>}{user.roles.includes('PARENT') && <span>Con: {user.studentIds.map(id => scopes.students.find(item => item.id === id)?.name ?? id).join(', ') || 'Chưa liên kết'}</span>}{!user.roles.includes('TEACHER') && !user.roles.includes('PARENT') && <span>{user.inspectorAccessUntil && !user.roles.length ? 'Chỉ đọc có hạn' : 'Phạm vi trường'}</span>}</td><td><span className={user.status === 'ACTIVE' ? 'status active' : 'status suspended'}>{user.status === 'ACTIVE' ? 'Hoạt động' : 'Tạm khóa'}</span></td><td><button type="button" className="icon-button" title={`Sửa ${user.fullName}`} aria-label={`Sửa ${user.fullName}`} onClick={() => { setResetTarget(null); setCurrent(user); setFormOpen(true); setTemporaryPassword('') }}><Pencil size={17} /></button><button type="button" className="icon-button" disabled={resetPending} title={`Đặt lại mật khẩu ${user.fullName}`} aria-label={`Đặt lại mật khẩu ${user.fullName}`} onClick={() => { setResetTarget(user); setResetReason(''); setFormOpen(false); setCurrent(null); setTemporaryPassword('') }}><KeyRound size={17} /></button></td></tr>)}</tbody></table>{!filtered.length && <div className="empty compact">Không có tài khoản phù hợp bộ lọc.</div>}</div>}
      <div className="pagination"><button type="button" disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>{page} / {Math.max(1, Math.ceil(total / 25))}</span><button type="button" disabled={page * 25 >= total} onClick={() => setPage(value => value + 1)}>Trang sau</button></div></section>
  </>
}
