import type { ParentRegistration } from './authApi'
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

export function validateParentRegistration(input: ParentRegistration, confirmation: string): string | null {
  if (input.fullName.trim().length < 2 || input.fullName.trim().length > 120) return 'Họ tên cần từ 2 đến 120 ký tự.'
  if (!normalizePhone(input.phoneNumber)) return 'SĐT Việt Nam không hợp lệ.'
  if (input.password.length < 12 || input.password.length > 128 || !/[A-Z]/.test(input.password) || !/[a-z]/.test(input.password) || !/[0-9]/.test(input.password) || !/[^a-zA-Z0-9]/.test(input.password)) return 'Mật khẩu cần 12–128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.'
  if (input.password !== confirmation) return 'Hai mật khẩu chưa khớp.'
  return null
}
