import { describe, expect, it } from 'vitest'
import { emptyDraft, type SchoolUser } from './model'
import { normalizePhone, validateUserDraft } from './validation'

describe('kiểm tra dữ liệu tài khoản FE', () => {
  const demoUsers: SchoolUser[] = [{ id: 'admin', fullName: 'Admin', email: 'admin@example.com', roles: ['ADMIN'], status: 'ACTIVE', classIds: [], studentIds: [], inspectorAccessUntil: null }]

  it('yêu cầu phạm vi lớp và học sinh cho vai trò tương ứng', () => {
    const draft = { ...emptyDraft(), fullName: 'Minh', email: 'minh@example.com', roles: ['TEACHER', 'PARENT'] as const }
    expect(validateUserDraft({ ...draft, roles: [...draft.roles] }, demoUsers, null)).toMatch(/lớp/)
    expect(validateUserDraft({ ...draft, roles: [...draft.roles], classIds: ['mam-1'] }, demoUsers, null)).toMatch(/học sinh/)
    expect(validateUserDraft({ ...draft, roles: [...draft.roles], classIds: ['mam-1'], studentIds: ['HS-2026-0012'] }, demoUsers, null)).toBeNull()
  })

  it('không đoán Admin cuối cùng từ một trang danh sách; BE kiểm tra toàn bộ DB', () => {
    const admin = demoUsers[0]
    expect(validateUserDraft({ ...admin, status: 'SUSPENDED' }, demoUsers, admin.id)).toBeNull()
  })

  it('cho phép tài khoản thanh tra chỉ đọc khi grant còn hạn', () => {
    const draft = { ...emptyDraft(), fullName: 'Thanh tra', email: 'inspector@example.com', inspectorAccessUntil: '2026-10-02' }
    expect(validateUserDraft(draft, demoUsers, null, new Date('2026-10-01T08:00:00'))).toBeNull()
  })
})

describe('phone account validation', () => {
  it('normalizes local and international formats and rejects invalid numbers', () => {
    expect(normalizePhone('+84 901 234 567')).toBe('0901234567')
    expect(normalizePhone('84901234567')).toBe('0901234567')
    expect(normalizePhone('123')).toBeNull()
  })
  it('accepts a parent without email and detects duplicate normalized phone', () => {
    const draft = { ...emptyDraft(), fullName: 'Parent', phoneNumber: '+84901234567', roles: ['PARENT'] as SchoolUser['roles'], studentIds: ['child'] }
    expect(validateUserDraft(draft, [], null)).toBeNull()
    expect(validateUserDraft(draft, [{ ...draft, id: 'existing', phoneNumber: '0901234567' }], null)).toMatch(/SĐT/)
  })
})
