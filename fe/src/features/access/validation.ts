import type { ParentRegistration } from './authApi'
import type { SchoolUser, UserDraft } from './model'

export function validateUserDraft(draft: UserDraft, users: SchoolUser[], currentId: string | null, now = new Date()): string | null {
  const name = draft.fullName.trim()
  const email = draft.email.trim().toLowerCase()
  if (!name) return 'Vui lòng nhập họ tên.'
  if (!email && !draft.phoneNumber?.trim()) return 'Cần SĐT hoặc email đăng nhập.'
  if (draft.phoneNumber?.trim() && !normalizePhone(draft.phoneNumber)) return 'SĐT không hợp lệ.'
  const emailError = validateEmail(email, users, currentId)
  if (emailError) return emailError
  if (draft.phoneNumber && isDuplicatePhone(draft.phoneNumber, users, currentId)) return 'SĐT này đã thuộc một tài khoản khác.'
  if (!draft.roles.length && !draft.inspectorAccessUntil) return 'Chọn ít nhất một vai trò hoặc cấp quyền thanh tra có hạn.'
  if (draft.roles.includes('TEACHER') && !draft.classIds.length) return 'Giáo viên cần được phân công ít nhất một lớp.'
  if (draft.inspectorAccessUntil && new Date(`${draft.inspectorAccessUntil}T23:59:59`).getTime() < now.getTime()) return 'Ngày hết hạn quyền thanh tra phải là hôm nay hoặc sau đó.'
  // The server checks the last active admin against all accounts, not one page.
  return null
}

function validateEmail(email: string, users: SchoolUser[], currentId: string | null): string | null {
  if (!email) return null
  if (!isValidEmail(email)) return 'Email không hợp lệ.'
  if (isDuplicateEmail(email, users, currentId)) return 'Email này đã thuộc một tài khoản khác.'
  return null
}

function isValidEmail(email: string): boolean {
  const separator = email.indexOf('@')
  if (separator <= 0 || separator !== email.lastIndexOf('@') || email.length > 254) return false
  const local = email.slice(0, separator)
  const domain = email.slice(separator + 1)
  if (local.length > 64 || local.startsWith('.') || local.endsWith('.') || local.includes('..')) return false
  if (!/^[A-Za-z0-9.!#$%&'*+/=?^_`{|}~-]+$/.test(local)) return false
  const labels = domain.split('.')
  if (labels.length < 2) return false
  return labels.every(label => label.length > 0 && label.length <= 63
    && /^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$/.test(label))
}

function isDuplicateEmail(email: string, users: SchoolUser[], currentId: string | null): boolean {
  return users.some(user => user.id !== currentId && user.email.toLowerCase() === email)
}

function isDuplicatePhone(phone: string, users: SchoolUser[], currentId: string | null): boolean {
  const normalized = normalizePhone(phone)
  return users.some(user => user.id !== currentId && user.phoneNumber && normalizePhone(user.phoneNumber) === normalized)
}

export function normalizePhone(value: string): string | null {
  let phone = value.trim().replace(/[\s().-]/g, '')
  if (phone.startsWith('+84')) phone = '0' + phone.slice(3)
  else if (phone.startsWith('84')) phone = '0' + phone.slice(2)
  return /^0[1-9]\d{8}$/.test(phone) ? phone : null
}

export function validateParentRegistration(input: ParentRegistration, confirmation: string): string | null {
  if (input.fullName.trim().length < 2 || input.fullName.trim().length > 120) return 'Họ tên cần từ 2 đến 120 ký tự.'
  if (!normalizePhone(input.phoneNumber)) return 'SĐT Việt Nam không hợp lệ.'
  if (input.password.length < 12 || input.password.length > 128 || !/[A-Z]/.test(input.password) || !/[a-z]/.test(input.password) || !/\d/.test(input.password) || !/[^a-zA-Z0-9]/.test(input.password)) return 'Mật khẩu cần 12–128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.'
  if (input.password !== confirmation) return 'Hai mật khẩu chưa khớp.'
  return null
}
