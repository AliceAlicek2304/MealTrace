import { Component, lazy, Suspense, useEffect, useRef, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { CalendarDays, Leaf, LogOut, ShieldCheck, UserRound, Users, School, ClipboardList, CalendarOff, Menu } from 'lucide-react'
import { Modal } from './components/Modal'
import { api, apiErrorMessage, getAccessToken, setAccessToken } from './lib/api'
import { canOpenPage, defaultPage, pageFromHash, usePageNavigation } from './lib/navigation'
import type { CurrentUser, LoginResponse } from './features/access/authApi'
import { LoginPage } from './features/access/LoginPage'
import { ProfilePage } from './features/access/ProfilePage'

const KitchenWorkspace = lazy(() => import('./features/kitchen/KitchenWorkspace').then(module => ({ default: module.KitchenWorkspace })))
const KitchenSourcesPage = lazy(() => import('./features/kitchen/KitchenSourcesPage').then(module => ({ default: module.KitchenSourcesPage })))
const KitchenMenusPage = lazy(() => import('./features/kitchen/KitchenMenusPage').then(module => ({ default: module.KitchenMenusPage })))
const ParentMenusPage = lazy(() => import('./features/kitchen/ParentMenusPage').then(module => ({ default: module.ParentMenusPage })))
const AdminToolsPage = lazy(() => import('./features/access/AdminToolsPage').then(module => ({ default: module.AdminToolsPage })))
const KitchenSettlementsPage = lazy(() => import('./features/kitchen/KitchenSettlementsPage').then(module => ({ default: module.KitchenSettlementsPage })))
const ParentLinksPage = lazy(() => import('./features/workflow/ParentLinksPage').then(module => ({ default: module.ParentLinksPage })))
const AccountsPage = lazy(() => import('./features/access/AccountsPage').then(module => ({ default: module.AccountsPage })))
const MealDaysPage = lazy(() => import('./features/meals/MealDaysPage').then(module => ({ default: module.MealDaysPage })))
const ClassesPage = lazy(() => import('./features/workflow/ClassesPage').then(module => ({ default: module.ClassesPage })))
const AbsencesPage = lazy(() => import('./features/workflow/AbsencesPage').then(module => ({ default: module.AbsencesPage })))
const PortionsPage = lazy(() => import('./features/workflow/PortionsPage').then(module => ({ default: module.PortionsPage })))
const MealCalendarPage = lazy(() => import('./features/workflow/MealCalendarPage').then(module => ({ default: module.MealCalendarPage })))

class PageLoadBoundary extends Component<Readonly<{ children: ReactNode }>, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  render() {
    if (this.state.failed) return <div role="alert" className="empty compact"><p>Chưa mở được chức năng. Vui lòng tải lại trang hoặc chọn mục khác.</p><button type="button" className="button secondary" onClick={() => window.location.reload()}>Tải lại trang</button></div>
    return this.props.children
  }
}

const mealRoles = new Set(['ADMIN', 'KITCHEN_STAFF'])
const pageLabels: Record<NonNullable<ReturnType<typeof usePageNavigation>[0]>, string> = {
  catalog: 'Nguyên liệu & công thức', audit: 'Lịch sử xử lý', kitchen: 'Tiếp nhận suất ăn', links: 'Liên kết trẻ', accounts: 'Quản lý tài khoản', classes: 'Lớp và trẻ', calendar: 'Lịch bữa ăn',
  portions: 'Số suất', absences: 'Báo vắng', meals: 'Theo dõi bữa ăn', profile: 'Thông tin cá nhân',
  sources: 'Nguồn nguyên liệu', menus: 'Thực đơn', 'parent-menus': 'Thực đơn của trẻ',
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
  if (!user) return <LoginPage onLogin={onLogin} />
  const pageName = page ? pageLabels[page] : pageLabels.profile

  const navProps = { page, isAdmin, canRegisterStudents, canSeePortions, isParent, isMealStaff, onNavigate: (nextPage: NonNullable<typeof page>) => { setPage(nextPage); setMenuOpen(false) } }

  return <div className="shell">
    <button ref={menuButtonRef} type="button" className="mobile-menu-toggle" aria-label="Mở menu" aria-expanded={menuOpen} aria-controls="mobile-navigation" onClick={() => setMenuOpen(true)}><Menu size={23} /></button>
    {menuOpen && <Modal drawer title="MealTrace" onClose={() => { setMenuOpen(false); menuButtonRef.current?.focus() }}><AppNavigation {...navProps} id="mobile-navigation" /></Modal>}
    <aside className="sidebar">
      <div className="brand"><span className="logo"><Leaf size={22} /></span><span>meal<b>trace</b><small>Quản lý bữa ăn bán trú</small></span></div>
      <p className="side-label">NGHIỆP VỤ NHÀ TRƯỜNG</p>
      <AppNavigation {...navProps} id="main-navigation" />
      <div className="side-foot"><ShieldCheck size={17} /> Có thể tra cứu lịch sử xử lý</div>
    </aside>
    <div className="content">
      <header><span>MealTrace / {pageName}</span><span className="user-actions"><span>{user.fullName}</span><button type="button" onClick={() => { void logout() }}><LogOut size={16} /> Đăng xuất</button></span></header>
      <main><PageLoadBoundary key={page}><Suspense fallback={<output aria-live="polite" className="empty compact">Đang mở chức năng…</output>}><PageContent page={page} user={user} onPasswordChanged={clearSession} /></Suspense></PageLoadBoundary></main>
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

function AppNavigation({ id, page, isAdmin, canRegisterStudents, canSeePortions, isParent, isMealStaff, onNavigate }: Readonly<NavigationProps>) {
  const items = [
    { id: 'accounts', label: 'Quản lý tài khoản', icon: Users, visible: isAdmin },
    { id: 'classes', label: 'Lớp và trẻ', icon: School, visible: canRegisterStudents },
    { id: 'calendar', label: 'Lịch bữa ăn', icon: CalendarDays, visible: isAdmin },
    { id: 'portions', label: 'Số suất', icon: ClipboardList, visible: canSeePortions },
    { id: 'absences', label: 'Báo vắng', icon: CalendarOff, visible: isParent },
    { id: 'meals', label: 'Theo dõi bữa ăn', icon: CalendarDays, visible: isMealStaff },
    { id: 'kitchen', label: 'Tiếp nhận suất ăn', icon: ClipboardList, visible: isMealStaff },
    { id: 'catalog', label: 'Nguyên liệu & công thức', icon: Leaf, visible: isMealStaff },
    { id: 'sources', label: 'Nguồn nguyên liệu', icon: Leaf, visible: isMealStaff },
    { id: 'menus', label: 'Thực đơn', icon: CalendarDays, visible: isMealStaff },
    { id: 'parent-menus', label: 'Thực đơn của trẻ', icon: CalendarDays, visible: isParent },
    { id: 'audit', label: 'Lịch sử xử lý', icon: ClipboardList, visible: isAdmin },
    { id: 'links', label: 'Liên kết trẻ', icon: Users, visible: isParent || canRegisterStudents },
    { id: 'profile', label: 'Thông tin cá nhân', icon: UserRound, visible: true },
  ] as const
  return <nav id={id} aria-label="Điều hướng chính">
    {items.filter(item => item.visible).map(item => {
      const Icon = item.icon
      return <button key={item.id} type="button" className={`nav ${page === item.id ? 'active' : ''}`} onClick={() => onNavigate(item.id)}><Icon size={18} /> {item.label}</button>
    })}
  </nav>
}

function PageContent({ page, user, onPasswordChanged }: Readonly<{
  page: ReturnType<typeof usePageNavigation>[0]
  user: CurrentUser
  onPasswordChanged: () => void
}>) {
  const profile = <ProfilePage user={user} onPasswordChanged={onPasswordChanged} />
  if (page && !canOpenPage(page, user.roles)) return profile
  const isAdmin = user.roles.includes('ADMIN')
  switch (page) {
    case 'catalog': return <KitchenWorkspace />
    case 'sources': return <KitchenSourcesPage />
    case 'menus': return <KitchenMenusPage isAdmin={isAdmin} />
    case 'parent-menus': return <ParentMenusPage />
    case 'audit': return <AdminToolsPage />
    case 'kitchen': return <KitchenSettlementsPage canAcknowledge={!isAdmin && user.roles.includes('KITCHEN_STAFF')} />
    case 'accounts': return <AccountsPage />
    case 'classes': return <ClassesPage isAdmin={isAdmin} />
    case 'calendar': return <MealCalendarPage />
    case 'portions': return <PortionsPage roles={user.roles} />
    case 'absences': return <AbsencesPage />
    case 'meals': return <MealDaysPage />
    case 'links': return <ParentLinksPage roles={user.roles} />
    default: return profile
  }
}
