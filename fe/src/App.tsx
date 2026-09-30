import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { CalendarDays, Leaf, LogOut, ShieldCheck, UserRound, Users } from 'lucide-react'
import { api, setAccessToken } from './lib/api'
import { AccountsPage } from './features/access/AccountsPage'
import type { CurrentUser, LoginResponse } from './features/access/authApi'
import { LoginPage } from './features/access/LoginPage'
import { ProfilePage } from './features/access/ProfilePage'
import { MealDaysPage } from './features/meals/MealDaysPage'

type Page = 'accounts' | 'meals' | 'profile'
const mealRoles = ['ADMIN', 'KITCHEN_STAFF', 'NUTRITIONIST', 'ACCOUNTANT']

export default function App() {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [page, setPage] = useState<Page>('profile')
  const isAdmin = user?.roles.includes('ADMIN') ?? false
  const isMealStaff = user?.roles.some(role => mealRoles.includes(role)) ?? false

  function clearSession() {
    setAccessToken(null)
    setUser(null)
    setPage('profile')
    queryClient.clear()
  }

  async function logout() {
    try { await api.post('/auth/logout') }
    catch { /* Clear the local session even when the API is unreachable. */ }
    finally { clearSession() }
  }

  useEffect(() => {
    const interceptor = api.interceptors.response.use(undefined, error => {
      if (error.response?.status === 401 && user) clearSession()
      return Promise.reject(error)
    })
    return () => api.interceptors.response.eject(interceptor)
  }, [user])

  function onLogin(result: LoginResponse) {
    setAccessToken(result.accessToken)
    setUser(result.user)
    setPage(result.user.roles.includes('ADMIN') ? 'accounts'
      : result.user.roles.some(role => mealRoles.includes(role)) ? 'meals' : 'profile')
  }

  if (!user) return <LoginPage onLogin={onLogin} />
  const pageName = page === 'accounts' ? 'Tài khoản' : page === 'meals' ? 'Ngày ăn' : 'Hồ sơ'

  return <div className="shell">
    <aside className="sidebar">
      <div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>School meal operations</small></span></div>
      <p className="side-label">KHÔNG GIAN LÀM VIỆC</p>
      <nav aria-label="Điều hướng chính">
        {isAdmin && <button className={`nav ${page === 'accounts' ? 'active' : ''}`} onClick={() => setPage('accounts')}><Users size={18} /> Tài khoản</button>}
        {isMealStaff && <button className={`nav ${page === 'meals' ? 'active' : ''}`} onClick={() => setPage('meals')}><CalendarDays size={18} /> Ngày ăn</button>}
        <button className={`nav ${page === 'profile' ? 'active' : ''}`} onClick={() => setPage('profile')}><UserRound size={18} /> Hồ sơ của tôi</button>
      </nav>
      <div className="side-foot"><ShieldCheck size={17} /> Dữ liệu có thể truy vết</div>
    </aside>
    <div className="content">
      <header><span>MealTrace / {pageName}</span><span className="user-actions"><span>{user.fullName}</span><button type="button" onClick={() => { void logout() }}><LogOut size={16} /> Đăng xuất</button></span></header>
      <main>{page === 'accounts' && isAdmin ? <AccountsPage />
        : page === 'meals' && isMealStaff ? <MealDaysPage />
        : <ProfilePage user={user} onPasswordChanged={clearSession} />}</main>
    </div>
  </div>
}
