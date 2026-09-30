import axios from 'axios'

let accessToken: string | null = null

export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api' })
api.interceptors.request.use(config => {
  if (accessToken) config.headers.Authorization = `Bearer ${accessToken}`
  return config
})

export function setAccessToken(token: string | null) { accessToken = token }

export function apiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    if (error.response?.status === 401) return 'Phiên đăng nhập không hợp lệ hoặc đã hết hạn.'
    if (error.response?.status === 403) return 'Tài khoản không có quyền thực hiện thao tác này.'
    if (error.response?.status === 429) return 'Thử đăng nhập quá nhanh. Vui lòng đợi một phút.'
    return error.response?.data?.message ?? 'Không kết nối được máy chủ.'
  }
  return 'Có lỗi xảy ra. Vui lòng thử lại.'
}
