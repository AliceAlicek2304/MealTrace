import type { SchoolUser, UserDraft } from './model'

export function validateUserDraft(draft: UserDraft, users: SchoolUser[], currentId: string | null, now = new Date()): string | null {
  const name = draft.fullName.trim()
  const email = draft.email.trim().toLowerCase()
  if (!name) return 'Vui lòng nhập họ tên.'
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return 'Email không hợp lệ.'
  if (users.some(user => user.id !== currentId && user.email.toLowerCase() === email)) return 'Email này đã thuộc một tài khoản khác.'
  if (!draft.roles.length && !draft.inspectorAccessUntil) return 'Chọn ít nhất một vai trò hoặc cấp quyền thanh tra có hạn.'
  if (draft.roles.includes('TEACHER') && !draft.classIds.length) return 'Giáo viên cần được phân công ít nhất một lớp.'
  if (draft.roles.includes('PARENT') && !draft.studentIds.length) return 'Phụ huynh cần được liên kết ít nhất một học sinh.'
  if (draft.inspectorAccessUntil && new Date(`${draft.inspectorAccessUntil}T23:59:59`).getTime() < now.getTime()) return 'Ngày hết hạn quyền thanh tra phải là hôm nay hoặc sau đó.'
  const current = users.find(user => user.id === currentId)
  const isLastActiveAdmin = current?.status === 'ACTIVE' && current.roles.includes('ADMIN')
    && !users.some(user => user.id !== currentId && user.status === 'ACTIVE' && user.roles.includes('ADMIN'))
  if (isLastActiveAdmin && (draft.status !== 'ACTIVE' || !draft.roles.includes('ADMIN'))) return 'Cần giữ lại ít nhất một Admin đang hoạt động.'
  return null
}
