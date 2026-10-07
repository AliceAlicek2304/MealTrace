import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { CalendarDays, Leaf, LogOut, ShieldCheck, UserRound, Users, School, ClipboardList, CalendarOff, Menu } from 'lucide-react'
import { Modal } from './components/Modal'
import { api, apiErrorMessage, getAccessToken, setAccessToken } from './lib/api'
import { canOpenPage, defaultPage, pageFromHash, usePageNavigation } from './lib/navigation'
import { AccountsPage } from './features/access/AccountsPage'
import type { CurrentUser, LoginResponse } from './features/access/authApi'
import { LoginPage } from './features/access/LoginPage'
import { ProfilePage } from './features/access/ProfilePage'
import { MealDaysPage } from './features/meals/MealDaysPage'
import { ClassesPage } from './features/workflow/ClassesPage'
import { AbsencesPage } from './features/workflow/AbsencesPage'
import { PortionsPage } from './features/workflow/PortionsPage'
import { MealCalendarPage } from './features/workflow/MealCalendarPage'
import { LandingPage } from './features/landing/LandingPage'

const mealRoles = ['ADMIN', 'KITCHEN_STAFF']

export default function App() {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [page, setPage] = usePageNavigation()
  const [menuOpen, setMenuOpen] = useState(false)
  const menuButtonRef = useRef<HTMLButtonElement>(null)
  const [restoring, setRestoring] = useState(!!getAccessToken())
  const [restoreError, setRestoreError] = useState('')
  const [restoreAttempt, setRestoreAttempt] = useState(0)
  const [showLogin, setShowLogin] = useState(() => !!pageFromHash())
  const isAdmin = user?.roles.includes('ADMIN') ?? false
  const canRegisterStudents = isAdmin || (user?.roles.includes('TEACHER') ?? false)
  const isMealStaff = user?.roles.some(role => mealRoles.includes(role)) ?? false
  const canSeePortions = user?.roles.some(role => ['ADMIN', 'TEACHER', 'KITCHEN_STAFF'].includes(role)) ?? false
  const isParent = user?.roles.includes('PARENT') ?? false

  function clearSession() {
    setAccessToken(null)
    setUser(null)
    setPage('profile', true)
    window.history.replaceState(null, '', window.location.pathname + window.location.search)
    setShowLogin(false)
    queryClient.clear()
  }

  async function logout() {
    try { await api.post('/auth/logout') }
    catch { /* Clear the local session even when the API is unreachable. */ }
    finally { clearSession() }
  }

  useEffect(() => {
    let active = true
    if (!getAccessToken()) { setRestoring(false); return }
    setRestoring(true)
    setRestoreError('')
    api.get<CurrentUser>('/auth/me').then(response => {
      if (active) setUser(response.data)
    }).catch(error => {
      if (!active) return
      if (error.response?.status === 401) setAccessToken(null)
      else setRestoreError(apiErrorMessage(error))
    }).finally(() => { if (active) setRestoring(false) })
    return () => { active = false }
  }, [restoreAttempt])

  useEffect(() => {
    if (user && (!page || !canOpenPage(page, user.roles))) setPage(defaultPage(user.roles), true)
  }, [user, page])

  useEffect(() => {
    setMenuOpen(false)
  }, [page])

  useEffect(() => {
    const desktop = window.matchMedia('(min-width: 761px)')
    const closeOnDesktop = () => { if (desktop.matches) setMenuOpen(false) }
    desktop.addEventListener('change', closeOnDesktop)
    return () => desktop.removeEventListener('change', closeOnDesktop)
  }, [])

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
    const requested = pageFromHash()
    setPage(requested && canOpenPage(requested, result.user.roles) ? requested : defaultPage(result.user.roles), true)
  }

  if (restoring) return <main className="session-status" role="status">Đang khôi phục phiên đăng nhập…</main>
  if (restoreError) return <main className="session-status"><h1>Chưa kết nối được máy chủ</h1><p role="alert">{restoreError}</p><button className="button primary" onClick={() => setRestoreAttempt(value => value + 1)}>Thử lại</button><button className="button secondary" onClick={() => { setRestoreError(''); clearSession() }}>Đăng nhập lại</button></main>
  if (!user) return showLogin ? <LoginPage onLogin={onLogin} onBack={() => setShowLogin(false)} /> : <LandingPage onLogin={() => setShowLogin(true)} />
  const pageName = page === 'accounts' ? 'Tài khoản' : page === 'classes' ? 'Lớp và trẻ' : page === 'calendar' ? 'Lịch bữa ăn' : page === 'portions' ? 'Số suất' : page === 'absences' ? 'Báo vắng' : page === 'meals' ? 'Ngày ăn' : 'Hồ sơ'

  const navigation = (id: string) => (
      <nav id={id} aria-label="Điều hướng chính" onClick={event => {
        if ((event.target as HTMLElement).closest('button')) setMenuOpen(false)
      }}>
        {isAdmin && <button type="button" className={`nav ${page === 'accounts' ? 'active' : ''}`} onClick={() => setPage('accounts')}><Users size={18} /> Tài khoản</button>}
        {canRegisterStudents && <button type="button" className={`nav ${page === 'classes' ? 'active' : ''}`} onClick={() => setPage('classes')}><School size={18} /> Lớp và trẻ</button>}
        {isAdmin && <button type="button" className={`nav ${page === 'calendar' ? 'active' : ''}`} onClick={() => setPage('calendar')}><CalendarDays size={18} /> Lịch bữa ăn</button>}
        {canSeePortions && <button type="button" className={`nav ${page === 'portions' ? 'active' : ''}`} onClick={() => setPage('portions')}><ClipboardList size={18} /> Số suất</button>}
        {isParent && <button type="button" className={`nav ${page === 'absences' ? 'active' : ''}`} onClick={() => setPage('absences')}><CalendarOff size={18} /> Báo vắng</button>}
        {isMealStaff && <button type="button" className={`nav ${page === 'meals' ? 'active' : ''}`} onClick={() => setPage('meals')}><CalendarDays size={18} /> Ngày ăn</button>}
        <button type="button" className={`nav ${page === 'profile' ? 'active' : ''}`} onClick={() => setPage('profile')}><UserRound size={18} /> Hồ sơ của tôi</button>
      </nav>
  )

  return <div className="shell">
    <button ref={menuButtonRef} type="button" className="mobile-menu-toggle" aria-label="Mở menu" aria-expanded={menuOpen} aria-controls="mobile-navigation" onClick={() => setMenuOpen(true)}><Menu size={23} /></button>
    {menuOpen && <Modal drawer title="MealTrace" onClose={() => { setMenuOpen(false); menuButtonRef.current?.focus() }}>{navigation('mobile-navigation')}</Modal>}
    <aside className="sidebar">
      <div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>School meal operations</small></span></div>
      <p className="side-label">KHÔNG GIAN LÀM VIỆC</p>
      {navigation('main-navigation')}
      <div className="side-foot"><ShieldCheck size={17} /> Dữ liệu có thể truy vết</div>
    </aside>
    <div className="content">
      <header><span>MealTrace / {pageName}</span><span className="user-actions"><span>{user.fullName}</span><button type="button" onClick={() => { void logout() }}><LogOut size={16} /> Đăng xuất</button></span></header>
      <main>{page === 'accounts' && isAdmin ? <AccountsPage />
        : page === 'classes' && canRegisterStudents ? <ClassesPage isAdmin={isAdmin} />
        : page === 'calendar' && isAdmin ? <MealCalendarPage />
        : page === 'portions' && canSeePortions ? <PortionsPage roles={user.roles} />
        : page === 'absences' && isParent ? <AbsencesPage />
        : page === 'meals' && isMealStaff ? <MealDaysPage />
        : <ProfilePage user={user} onPasswordChanged={clearSession} />}</main>
    </div>
  </div>
}
