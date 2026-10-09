import axios from 'axios'

const sessionKey = 'mealtrace.session'
function storedToken(): string | null {
  try { return sessionStorage.getItem(sessionKey) } catch { return null }
}
let accessToken: string | null = storedToken()

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api' })
api.interceptors.request.use(config => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

export function getAccessToken() { return accessToken }
export function setAccessToken(token: string | null) {
  accessToken = token
  try {
    if (token) sessionStorage.setItem(sessionKey, token)
    else sessionStorage.removeItem(sessionKey)
  } catch { /* Memory-only sessions remain available when browser storage is blocked. */ }
}

export function apiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 401) return 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.'
    if (error.response?.status === 403) return 'Tài khoản không có quyền thực hiện thao tác này.'
    if (error.response?.status === 429) return 'Thao tác quá nhanh. Vui lòng đợi một phút.'
    return error.response?.data?.message ?? 'Không kết nối được máy chủ.'
  }
  return 'Có lỗi xảy ra. Vui lòng thử lại.'
}
