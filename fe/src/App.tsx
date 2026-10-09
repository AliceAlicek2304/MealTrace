import { ParentLinksPage } from './features/workflow/ParentLinksPage'
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

const mealRoles = new Set(['ADMIN', 'KITCHEN_STAFF'])
const pageLabels: Record<NonNullable<ReturnType<typeof usePageNavigation>[0]>, string> = {
  links: 'Liên kết trẻ', accounts: 'Tài khoản', classes: 'Lớp và trẻ', calendar: 'Lịch bữa ăn',
  portions: 'Số suất', absences: 'Báo vắng', meals: 'Ngày ăn', profile: 'Hồ sơ',
}

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
  const isMealStaff = user?.roles.some(role => mealRoles.has(role)) ?? false
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

  if (restoring) return <output className="session-status" aria-live="polite">Đang khôi phục phiên đăng nhập…</output>
  if (restoreError) return <main className="session-status"><h1>Chưa kết nối được máy chủ</h1><p role="alert">{restoreError}</p><button type="button" className="button primary" onClick={() => setRestoreAttempt(value => value + 1)}>Thử lại</button><button type="button" className="button secondary" onClick={() => { setRestoreError(''); clearSession() }}>Đăng nhập lại</button></main>
  if (!user) {
    if (showLogin) return <LoginPage onLogin={onLogin} onBack={() => setShowLogin(false)} />
    return <LandingPage onLogin={() => setShowLogin(true)} />
  }
  const pageName = page ? pageLabels[page] : pageLabels.profile

  const navProps = { page, isAdmin, canRegisterStudents, canSeePortions, isParent, isMealStaff, onNavigate: (nextPage: NonNullable<typeof page>) => { setPage(nextPage); setMenuOpen(false) } }

  return <div className="shell">
    <button ref={menuButtonRef} type="button" className="mobile-menu-toggle" aria-label="Mở menu" aria-expanded={menuOpen} aria-controls="mobile-navigation" onClick={() => setMenuOpen(true)}><Menu size={23} /></button>
    {menuOpen && <Modal drawer title="MealTrace" onClose={() => { setMenuOpen(false); menuButtonRef.current?.focus() }}><AppNavigation {...navProps} id="mobile-navigation" /></Modal>}
    <aside className="sidebar">
      <div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>School meal operations</small></span></div>
      <p className="side-label">KHÔNG GIAN LÀM VIỆC</p>
      <AppNavigation {...navProps} id="main-navigation" />
      <div className="side-foot"><ShieldCheck size={17} /> Dữ liệu có thể truy vết</div>
    </aside>
    <div className="content">
      <header><span>MealTrace / {pageName}</span><span className="user-actions"><span>{user.fullName}</span><button type="button" onClick={() => { void logout() }}><LogOut size={16} /> Đăng xuất</button></span></header>
      <main><PageContent page={page} user={user} isAdmin={isAdmin} canRegisterStudents={canRegisterStudents}
        canSeePortions={canSeePortions} isParent={isParent} isMealStaff={isMealStaff} onPasswordChanged={clearSession} /></main>
    </div>
  </div>
}

type NavigationProps = {
  id: string
  page: ReturnType<typeof usePageNavigation>[0]
  isAdmin: boolean
  canRegisterStudents: boolean
  canSeePortions: boolean
  isParent: boolean
  isMealStaff: boolean
  onNavigate: (page: NonNullable<ReturnType<typeof usePageNavigation>[0]>) => void
}

function AppNavigation({ id, page, isAdmin, canRegisterStudents, canSeePortions, isParent, isMealStaff, onNavigate }: NavigationProps) {
  const items = [
    { id: 'accounts', label: 'Tài khoản', icon: Users, visible: isAdmin },
    { id: 'classes', label: 'Lớp và trẻ', icon: School, visible: canRegisterStudents },
    { id: 'calendar', label: 'Lịch bữa ăn', icon: CalendarDays, visible: isAdmin },
    { id: 'portions', label: 'Số suất', icon: ClipboardList, visible: canSeePortions },
    { id: 'absences', label: 'Báo vắng', icon: CalendarOff, visible: isParent },
    { id: 'meals', label: 'Ngày ăn', icon: CalendarDays, visible: isMealStaff },
    { id: 'links', label: 'Liên kết trẻ', icon: Users, visible: isParent || canRegisterStudents },
    { id: 'profile', label: 'Hồ sơ của tôi', icon: UserRound, visible: true },
  ] as const
  return <nav id={id} aria-label="Điều hướng chính">
    {items.filter(item => item.visible).map(item => {
      const Icon = item.icon
      return <button key={item.id} type="button" className={`nav ${page === item.id ? 'active' : ''}`} onClick={() => onNavigate(item.id)}><Icon size={18} /> {item.label}</button>
    })}
  </nav>
}

function PageContent({ page, user, isAdmin, canRegisterStudents, canSeePortions, isParent, isMealStaff, onPasswordChanged }: {
  page: ReturnType<typeof usePageNavigation>[0]
  user: CurrentUser
  isAdmin: boolean
  canRegisterStudents: boolean
  canSeePortions: boolean
  isParent: boolean
  isMealStaff: boolean
  onPasswordChanged: () => void
}) {
  switch (page) {
    case 'accounts': if (isAdmin) return <AccountsPage />; break
    case 'classes': if (canRegisterStudents) return <ClassesPage isAdmin={isAdmin} />; break
    case 'calendar': if (isAdmin) return <MealCalendarPage />; break
    case 'portions': if (canSeePortions) return <PortionsPage roles={user.roles} />; break
    case 'absences': if (isParent) return <AbsencesPage />; break
    case 'meals': if (isMealStaff) return <MealDaysPage />; break
    case 'links': if (isParent || canRegisterStudents) return <ParentLinksPage roles={user.roles} />; break
    default: break
  }
  return <ProfilePage user={user} onPasswordChanged={onPasswordChanged} />
}
