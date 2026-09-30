import { describe, expect, it } from 'vitest'
import { emptyDraft, type SchoolUser } from './model'
import { validateUserDraft } from './validation'

describe('kiểm tra dữ liệu tài khoản FE', () => {
  const demoUsers: SchoolUser[] = [{ id: 'admin', fullName: 'Admin', email: 'admin@example.com', roles: ['ADMIN'], status: 'ACTIVE', classIds: [], studentIds: [], inspectorAccessUntil: null }]

  it('yêu cầu phạm vi lớp và học sinh cho vai trò tương ứng', () => {
    const draft = { ...emptyDraft(), fullName: 'Minh', email: 'minh@example.com', roles: ['TEACHER', 'PARENT'] as const }
    expect(validateUserDraft({ ...draft, roles: [...draft.roles] }, demoUsers, null)).toMatch(/lớp/)
    expect(validateUserDraft({ ...draft, roles: [...draft.roles], classIds: ['mam-1'] }, demoUsers, null)).toMatch(/học sinh/)
    expect(validateUserDraft({ ...draft, roles: [...draft.roles], classIds: ['mam-1'], studentIds: ['HS-2026-0012'] }, demoUsers, null)).toBeNull()
  })

  it('không cho khóa Admin đang hoạt động cuối cùng trong phiên demo', () => {
    const admin = demoUsers[0]
    expect(validateUserDraft({ ...admin, status: 'SUSPENDED' }, demoUsers, admin.id)).toMatch(/Admin/)
  })

  it('cho phép tài khoản thanh tra chỉ đọc khi grant còn hạn', () => {
    const draft = { ...emptyDraft(), fullName: 'Thanh tra', email: 'inspector@example.com', inspectorAccessUntil: '2026-10-02' }
    expect(validateUserDraft(draft, demoUsers, null, new Date('2026-10-01T08:00:00'))).toBeNull()
  })
})
