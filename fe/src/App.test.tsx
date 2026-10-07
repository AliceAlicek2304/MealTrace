// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, getAccessToken, setAccessToken } from './lib/api'
import App from './App'

vi.mock('./features/workflow/ClassesPage', () => ({ ClassesPage: () => <h1>Danh sách lớp thử</h1> }))
vi.mock('./features/workflow/PortionsPage', () => ({ PortionsPage: () => <h1>Số suất thử</h1> }))
vi.mock('./features/access/ProfilePage', () => ({ ProfilePage: () => <h1>Hồ sơ thử</h1> }))
vi.mock('./features/landing/LandingPage', () => ({ LandingPage: () => <h1>Trang giới thiệu thử</h1> }))

const user = { id: 'teacher', fullName: 'Giáo viên A', roles: ['TEACHER'], email: 'teacher@example.test', inspectorAccessUntil: null }
let cache: QueryClient
function mount() {
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={cache}><App /></QueryClientProvider>)
}
afterEach(() => { cleanup(); cache.clear(); setAccessToken(null); window.history.replaceState(null, '', '/'); vi.restoreAllMocks() })

describe('Session restoration and navigation', () => {
  it('validates the saved session and opens the linked page after reload', async () => {
    setAccessToken('test-token')
    window.history.replaceState(null, '', '#/classes')
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: user })
    mount()
    expect(await screen.findByRole('heading', { name: 'Danh sách lớp thử' })).toBeTruthy()
    expect(get).toHaveBeenCalledWith('/auth/me')
    expect(sessionStorage.getItem('mealtrace.session')).toBe('test-token')
  })

  it('updates the URL and responds to browser history navigation', async () => {
    setAccessToken('test-token')
    window.history.replaceState(null, '', '#/classes')
    vi.spyOn(api, 'get').mockResolvedValue({ data: user })
    mount()
    await screen.findByRole('heading', { name: 'Danh sách lớp thử' })
    fireEvent.click(screen.getByRole('button', { name: 'Số suất' }))
    expect(window.location.hash).toBe('#/portions')
    expect(screen.getByRole('heading', { name: 'Số suất thử' })).toBeTruthy()
    window.location.hash = '/classes'
    window.dispatchEvent(new HashChangeEvent('hashchange'))
    expect(await screen.findByRole('heading', { name: 'Danh sách lớp thử' })).toBeTruthy()
  })

  it('redirects a teacher away from an admin page', async () => {
    setAccessToken('test-token')
    window.history.replaceState(null, '', '#/accounts')
    vi.spyOn(api, 'get').mockResolvedValue({ data: user })
    mount()
    await screen.findByRole('heading', { name: 'Số suất thử' })
    expect(window.location.hash).toBe('#/portions')
    expect(screen.queryByRole('button', { name: 'Tài khoản' })).toBeNull()
  })

  it('removes an expired saved token', async () => {
    setAccessToken('expired-token')
    vi.spyOn(api, 'get').mockRejectedValue({ response: { status: 401 } })
    mount()
    await screen.findByRole('heading', { name: 'Trang giới thiệu thử' })
    expect(getAccessToken()).toBeNull()
    expect(sessionStorage.getItem('mealtrace.session')).toBeNull()
  })

  it('preserves the session on a network failure and allows retrying', async () => {
    setAccessToken('test-token')
    vi.spyOn(api, 'get').mockRejectedValueOnce(new Error('offline')).mockResolvedValue({ data: user })
    mount()
    await screen.findByRole('heading', { name: 'Chưa kết nối được máy chủ' })
    expect(getAccessToken()).toBe('test-token')
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
    await screen.findByRole('heading', { name: 'Số suất thử' })
  })

  it('clears stored credentials and cached data on logout', async () => {
    setAccessToken('test-token')
    vi.spyOn(api, 'get').mockResolvedValue({ data: user })
    vi.spyOn(api, 'post').mockResolvedValue({})
    mount()
    await screen.findByRole('heading', { name: 'Số suất thử' })
    cache.setQueryData(['private'], 'data')
    fireEvent.click(screen.getByRole('button', { name: 'Đăng xuất' }))
    await waitFor(() => expect(getAccessToken()).toBeNull())
    expect(cache.getQueryData(['private'])).toBeUndefined()
    expect(sessionStorage.getItem('mealtrace.session')).toBeNull()
  })
})
