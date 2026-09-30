import type { SchoolUser, UserDraft } from './model'

export function validateUserDraft(draft: UserDraft, users: SchoolUser[], currentId: string | null, now = new Date()): string | null {
  const name = draft.fullName.trim()
  const email = draft.email.trim().toLowerCase()
  if (!name) return 'Vui lòng nhập họ tên.'
  if (!email && !draft.phoneNumber?.trim()) return 'Cần SĐT hoặc email đăng nhập.'
  if (draft.phoneNumber?.trim() && !normalizePhone(draft.phoneNumber)) return 'SĐT không hợp lệ.'
  if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) return 'Email không hợp lệ.'
  if (email && users.some(user => user.id !== currentId && user.email.toLowerCase() === email)) return 'Email này đã thuộc một tài khoản khác.'
  if (draft.phoneNumber && users.some(user => user.id !== currentId && user.phoneNumber && normalizePhone(user.phoneNumber) === normalizePhone(draft.phoneNumber!))) return 'SĐT này đã thuộc một tài khoản khác.'
  if (!draft.roles.length && !draft.inspectorAccessUntil) return 'Chọn ít nhất một vai trò hoặc cấp quyền thanh tra có hạn.'
  if (draft.roles.includes('TEACHER') && !draft.classIds.length) return 'Giáo viên cần được phân công ít nhất một lớp.'
  if (draft.roles.includes('PARENT') && !draft.studentIds.length) return 'Phụ huynh cần được liên kết ít nhất một học sinh.'
  if (draft.inspectorAccessUntil && new Date(`${draft.inspectorAccessUntil}T23:59:59`).getTime() < now.getTime()) return 'Ngày hết hạn quyền thanh tra phải là hôm nay hoặc sau đó.'
  // The server checks the last active admin against all accounts, not one page.
  return null
}

export function normalizePhone(value: string): string | null {
  let phone = value.trim().replace(/[\s().-]/g, '')
  if (phone.startsWith('+84')) phone = '0' + phone.slice(3)
  else if (phone.startsWith('84')) phone = '0' + phone.slice(2)
  return /^0[1-9][0-9]{8}$/.test(phone) ? phone : null
}
