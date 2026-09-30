export const roles = [
  { id: 'ADMIN', label: 'Ban giám hiệu / Admin', description: 'Quản trị trường, phê duyệt và phân quyền.' },
  { id: 'TEACHER', label: 'Giáo viên chủ nhiệm', description: 'Điểm danh và xác nhận suất ăn của lớp được giao.' },
  { id: 'KITCHEN_STAFF', label: 'Bếp & y tế', description: 'Thực hiện bữa ăn và ghi nhận kiểm thực.' },
  { id: 'PARENT', label: 'Phụ huynh', description: 'Thông tin và thao tác cho con được liên kết.' },
] as const

export type Role = (typeof roles)[number]['id']
export type UserStatus = 'ACTIVE' | 'SUSPENDED'

export type SchoolUser = {
  id: string
  fullName: string
  email: string
  phoneNumber?: string | null
  roles: Role[]
  status: UserStatus
  classIds: string[]
  studentIds: string[]
  inspectorAccessUntil: string | null
}

export type UserDraft = Omit<SchoolUser, 'id'>

export const emptyDraft = (): UserDraft => ({
  fullName: '', email: '', phoneNumber: '', roles: [], status: 'ACTIVE', classIds: [], studentIds: [], inspectorAccessUntil: null,
})
