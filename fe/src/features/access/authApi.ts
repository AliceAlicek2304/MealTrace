import { api } from '../../lib/api'
import type { SchoolUser, UserDraft } from './model'

export type CurrentUser = { id: string; fullName: string; email: string; phoneNumber?: string | null; roles: string[]; inspectorAccessUntil: string | null }
export type LoginResponse = { accessToken: string; expiresAt: string; user: CurrentUser }
export type ParentRegistration = { fullName: string; phoneNumber: string; password: string; challengeId?: string; otpCode?: string }
export type ParentOtpChallenge = { challengeId: string; expiresAt: string; resendAt: string; message: string }
export async function requestParentOtp(phoneNumber: string): Promise<ParentOtpChallenge> {
  return (await api.post<ParentOtpChallenge>('/auth/register/otp', { phoneNumber })).data
}
export async function registerParent(input: ParentRegistration): Promise<void> {
  await api.post('/auth/register', { fullName: input.fullName.trim(), phoneNumber: input.phoneNumber, password: input.password, challengeId: input.challengeId, otpCode: input.otpCode })
}
export type ScopeOptions = {
  classes: { id: string; name: string }[]
  students: { id: string; name: string; classId: string }[]
  selectedClasses?: { id: string; name: string }[]
  selectedStudents?: { id: string; name: string; classId: string }[]
  classTotal?: number
  studentTotal?: number
  pageSize?: number
}

export async function login(email: string, password: string): Promise<LoginResponse> {
  return (await api.post<LoginResponse>('/auth/login', { identifier: email, password })).data
}
export type UserPage = { items: SchoolUser[]; total: number; page: number; pageSize: number }
export async function listUsers(page: number, classId: string, search = '', role = 'ALL'): Promise<UserPage> {
  return (await api.get<UserPage>('/admin/users', { params: { page, pageSize: 25, classId: classId || undefined, search: search.trim() || undefined, role: role === 'ALL' ? undefined : role } })).data
}
export async function getScopeOptions(params: { search?: string; classId?: string; classPage?: number; studentPage?: number; selectedClassIds?: string; selectedStudentIds?: string } = {}): Promise<ScopeOptions> {
  return (await api.get<ScopeOptions>('/admin/scope-options', { params })).data
}
export async function createUser(draft: UserDraft): Promise<{ user: SchoolUser; temporaryPassword: string }> {
  return (await api.post<{ user: SchoolUser; temporaryPassword: string }>('/admin/users', draft)).data
}
export async function updateUser(id: string, draft: UserDraft): Promise<SchoolUser> {
  return (await api.put<SchoolUser>(`/admin/users/${id}`, draft)).data
}

export type ResetPasswordResult = { userId: string; fullName: string; phoneNumber: string | null; email: string | null; temporaryPassword: string; isActive: boolean }
export async function resetUserPassword(id: string, reason: string): Promise<ResetPasswordResult> {
  return (await api.post<ResetPasswordResult>(`/admin/users/${id}/reset-password`, { reason })).data
}
