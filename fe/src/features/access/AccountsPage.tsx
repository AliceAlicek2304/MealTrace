import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Pencil, Plus, Search, ShieldCheck, Users } from 'lucide-react'
import { apiErrorMessage } from '../../lib/api'
import { createUser, getScopeOptions, listUsers, updateUser } from './authApi'
import { roles, type Role, type UserDraft } from './model'
import { UserForm } from './UserForm'

const roleName = (id: Role) => roles.find(role => role.id === id)?.label ?? id

export function AccountsPage() {
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [roleFilter, setRoleFilter] = useState<Role | 'ALL'>('ALL')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [notice, setNotice] = useState('')
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const userQuery = useQuery({ queryKey: ['admin-users', page], queryFn: () => listUsers(page) })
  const scopeQuery = useQuery({ queryKey: ['scope-options'], queryFn: getScopeOptions })
  const users = userQuery.data?.items ?? []
  const scopes = scopeQuery.data ?? { classes: [], students: [] }
  const current = users.find(user => user.id === editingId) ?? null
  const filtered = useMemo(() => users.filter(user => {
    const matchesText = `${user.fullName} ${user.email}`.toLocaleLowerCase('vi-VN').includes(search.toLocaleLowerCase('vi-VN').trim())
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
        setNotice('Đã cập nhật tài khoản trong database.')
      } else {
        const result = await createUser(cleaned)
        setTemporaryPassword(result.temporaryPassword)
        setNotice(`Đã tạo ${result.user.email}. Mật khẩu tạm chỉ hiển thị một lần dưới đây.`)
        setPage(1)
      }
      await queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      setFormOpen(false)
      setEditingId(null)
    } catch (error) { throw new Error(apiErrorMessage(error)) }
  }

  const total = userQuery.data?.total ?? 0
  return <><div className="page-head"><div><div className="eyebrow">FR-02 · ET-05 / ET-06 / ET-07</div><h1>Quản lý tài khoản</h1><p className="lead">Gán nhiều vai trò và giới hạn dữ liệu theo lớp, học sinh hoặc quyền thanh tra có thời hạn.</p></div><button className="button primary" onClick={() => { setEditingId(null); setFormOpen(true); setNotice(''); setTemporaryPassword('') }}><Plus size={17} /> Thêm tài khoản</button></div>
    <div className="demo-banner"><ShieldCheck size={20} /><div><strong>Dữ liệu tài khoản từ PostgreSQL</strong><span>Chỉ Admin truy cập API này. Mật khẩu tạm của tài khoản mới được hiển thị một lần và không lưu ở FE.</span></div></div>
    <div className="stats"><div><span>Tổng tài khoản</span><strong>{userQuery.isPending ? '—' : total}</strong></div><div><span>Đang hoạt động trong trang</span><strong>{userQuery.isPending ? '—' : users.filter(user => user.status === 'ACTIVE').length}</strong></div><div><span>Đa vai trò trong trang</span><strong>{userQuery.isPending ? '—' : users.filter(user => user.roles.length > 1).length}</strong></div></div>
    {notice && <div className="notice" role="status">{notice}</div>}
    {temporaryPassword && <div className="credential-once"><strong>Mật khẩu tạm:</strong> <code>{temporaryPassword}</code><button type="button" onClick={() => setTemporaryPassword('')}>Đã lưu, ẩn mật khẩu</button><small>Hãy chuyển riêng cho người dùng qua kênh an toàn. Tải lại trang sẽ không xem lại được.</small></div>}
    {formOpen && <section className="panel form-panel"><div className="panel-head"><div><h2>{current ? `Chỉnh sửa ${current.fullName}` : 'Thêm tài khoản mới'}</h2><p>Vai trò và phạm vi sẽ được BE kiểm tra trước khi lưu.</p></div></div>{scopeQuery.isPending ? <div className="empty compact">Đang tải lớp và học sinh…</div> : scopeQuery.isError ? <div className="empty compact error">Không tải được danh mục phạm vi.</div> : <UserForm key={editingId ?? 'new'} current={current} users={users} scopes={scopes} onSave={save} onCancel={() => { setFormOpen(false); setEditingId(null) }} />}</section>}
    <section className="panel"><div className="panel-head"><div><h2>Danh sách tài khoản</h2><p>{total} tài khoản · trang {page}</p></div><Users size={20} /></div><div className="toolbar"><label className="search"><Search size={17} /><input aria-label="Tìm tài khoản trong trang" placeholder="Tìm trong trang hiện tại" value={search} onChange={event => setSearch(event.target.value)} /></label><select aria-label="Lọc theo vai trò trong trang" value={roleFilter} onChange={event => setRoleFilter(event.target.value as Role | 'ALL')}><option value="ALL">Tất cả vai trò</option>{roles.map(role => <option key={role.id} value={role.id}>{role.label}</option>)}</select></div>
      {userQuery.isPending ? <div className="empty compact">Đang tải tài khoản…</div> : userQuery.isError ? <div className="empty compact error">{apiErrorMessage(userQuery.error)}</div> : <div className="table-wrap"><table><thead><tr><th>Tài khoản</th><th>Vai trò</th><th>Phạm vi</th><th>Trạng thái</th><th></th></tr></thead><tbody>{filtered.map(user => <tr key={user.id}><td><strong>{user.fullName}</strong><small>{user.email}</small></td><td><div className="chips">{user.roles.map(role => <span key={role}>{roleName(role)}</span>)}{user.inspectorAccessUntil && <span className="inspector-chip">Thanh tra đến {user.inspectorAccessUntil}</span>}</div></td><td className="scope-cell">{user.roles.includes('TEACHER') && <span>Lớp: {user.classIds.map(id => scopes.classes.find(item => item.id === id)?.name ?? id).join(', ') || 'Chưa gán'}</span>}{user.roles.includes('PARENT') && <span>Con: {user.studentIds.map(id => scopes.students.find(item => item.id === id)?.name ?? id).join(', ') || 'Chưa liên kết'}</span>}{!user.roles.includes('TEACHER') && !user.roles.includes('PARENT') && <span>{user.inspectorAccessUntil && !user.roles.length ? 'Chỉ đọc có hạn' : 'Phạm vi trường'}</span>}</td><td><span className={user.status === 'ACTIVE' ? 'status active' : 'status suspended'}>{user.status === 'ACTIVE' ? 'Hoạt động' : 'Tạm khóa'}</span></td><td><button className="icon-button" title={`Sửa ${user.fullName}`} aria-label={`Sửa ${user.fullName}`} onClick={() => { setEditingId(user.id); setFormOpen(true); setNotice(''); setTemporaryPassword('') }}><Pencil size={17} /></button></td></tr>)}</tbody></table>{!filtered.length && <div className="empty compact">Không có tài khoản phù hợp bộ lọc.</div>}</div>}
      <div className="pagination"><button disabled={page <= 1} onClick={() => setPage(value => value - 1)}>Trang trước</button><span>{page} / {Math.max(1, Math.ceil(total / 25))}</span><button disabled={page * 25 >= total} onClick={() => setPage(value => value + 1)}>Trang sau</button></div></section>
  </>
}
