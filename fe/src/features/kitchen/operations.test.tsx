// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AxiosError } from 'axios'
import { toast } from 'sonner'
import type { ReactNode } from 'react'
import { api } from '../../lib/api'
import { KitchenMenusPage } from './KitchenMenusPage'
import { KitchenSourcesPage } from './KitchenSourcesPage'
import { ParentMenusPage } from './ParentMenusPage'
import type { Menu, MenuState } from './operationsApi'

vi.mock('sonner', () => ({ Toaster: () => null, toast: { success: vi.fn(), error: vi.fn() } }))

const date = '2026-11-27'
const timestamp = `${date}T05:00:00Z`
const dish = { recipeId: 'recipe-1', recipeVersionId: 'recipe-v2', recipeName: 'Canh bí đỏ', recipeVersion: 2, plannedPortions: null }
const supplier = { id: 'supplier-1', versionId: 'supplier-v2', version: 2, name: 'Nguồn A', contact: null, reference: null, recordedAt: timestamp }
const receipt = { id: 'receipt-1', ingredientId: 'ingredient-1', ingredientName: 'Bí đỏ', unit: 'g', supplierVersionId: 'supplier-v1', supplierName: 'Tên cũ', quantity: 600, referenceDate: date, version: 3, versionId: 'receipt-v3', deliveryRef: 'PG-01', lotRef: null, recordedAt: timestamp }
const responses = {
  childA: { studentId: 'child-1', fullName: 'Trẻ A', className: 'A1' },
  childB: { studentId: 'child-2', fullName: 'Trẻ B', className: 'A2' },
}
function page<T>(items: T[]) { return { items, total: items.length, page: 1, pageSize: 20 } }
function menu(state: MenuState): Menu {
  return { id: 'menu-1', date, mealDayId: 'day-1', mealType: 'Lunch', publishedVersionId: state === 'published' ? 'menu-v2' : null, publishedVersion: state === 'published' ? 2 : null,
    latest: { state, dishes: [dish], reason: null, versionId: 'menu-v2', version: 2, recordedAt: timestamp, transitions: [{ state, sequence: 1, reason: null, at: timestamp }] } }
}
let cache: QueryClient
function mount(element: ReactNode) { render(<QueryClientProvider client={cache}>{element}</QueryClientProvider>) }
function conflict(code: string) {
  const error = new AxiosError(code)
  error.response = { status: 409, data: { code } } as NonNullable<AxiosError['response']>
  return error
}
beforeEach(() => {
  vi.clearAllMocks()
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })

it('creates a source receipt from the selected supplier revision and ingredient unit', async () => {
  vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url === '/kitchen/ingredient-source-entries') return { data: page([]) }
    if (url === '/kitchen/ingredients') return { data: page([{ id: 'ingredient-1', name: 'Bí đỏ', unit: 'g' }]) }
    if (url === '/kitchen/suppliers') return { data: page([supplier]) }
    throw new Error(url)
  })
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
  mount(<KitchenSourcesPage />)
  fireEvent.click(screen.getByRole('button', { name: 'Thêm phiếu nguồn' }))
  const dialog = within(screen.getByRole('dialog'))
  fireEvent.change(await dialog.findByLabelText('Nguyên liệu'), { target: { value: 'ingredient-1' } })
  fireEvent.change(await dialog.findByLabelText('Nhà cung cấp'), { target: { value: 'supplier-v2' } })
  fireEvent.change(dialog.getByLabelText('Ngày nhận'), { target: { value: date } })
  fireEvent.change(dialog.getByLabelText('Số lượng (g)'), { target: { value: '600' } })
  fireEvent.click(dialog.getByRole('button', { name: 'Lưu' }))
  await waitFor(() => expect(post).toHaveBeenCalledExactlyOnceWith('/kitchen/ingredient-source-entries', {
    ingredientId: 'ingredient-1', supplierVersionId: 'supplier-v2', referenceDate: date, quantity: 600, unit: 'g', deliveryRef: null, lotRef: null,
  }))
  await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
})

it('keeps the original supplier revision and an unsaved correction after a conflict', async () => {
  vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url.endsWith('/history')) return { data: { items: [{ ...receipt, reason: 'Đã kiểm tra phiếu gốc' }] } }
    if (url === '/kitchen/ingredient-source-entries') return { data: page([receipt]) }
    if (url === '/kitchen/suppliers') return { data: page([supplier]) }
    return { data: page([]) }
  })
  const post = vi.spyOn(api, 'post').mockRejectedValue(conflict('SOURCE_ENTRY_VERSION_CONFLICT'))
  mount(<KitchenSourcesPage />)
  fireEvent.click(await screen.findByRole('button', { name: 'Chi tiết' }))
  const dialog = within(screen.getByRole('dialog'))
  expect(await dialog.findByText(/Đã kiểm tra phiếu gốc/)).toBeTruthy()
  fireEvent.click(dialog.getByRole('button', { name: 'Sửa phiếu nguồn' }))
  const selector = await dialog.findByLabelText('Nhà cung cấp') as HTMLSelectElement
  expect(selector.value).toBe('supplier-v1')
  fireEvent.change(dialog.getByLabelText('Lý do sửa'), { target: { value: 'Đối chiếu cân' } })
  fireEvent.change(dialog.getByLabelText('Số lượng (g)'), { target: { value: '610' } })
  fireEvent.click(dialog.getByRole('button', { name: 'Lưu' }))
  await waitFor(() => expect(toast.error).toHaveBeenCalled())
  expect(post).toHaveBeenCalledExactlyOnceWith('/kitchen/ingredient-source-entries/receipt-1/versions', {
    supplierVersionId: 'supplier-v1', referenceDate: date, quantity: 610, unit: 'g', deliveryRef: 'PG-01', lotRef: null,
    expectedVersion: 3, reason: 'Đối chiếu cân',
  })
  expect((dialog.getByLabelText('Số lượng (g)') as HTMLInputElement).value).toBe('610')
})

it('saves a menu with the chosen recipe version and submits the saved draft as kitchen staff', async () => {
  let saved = false
  let submitted = false
  vi.spyOn(api, 'get').mockImplementation(async url => {
    switch (url) {
      case '/kitchen/menus': return { data: page(saved ? [menu(submitted ? 'submitted' : 'draft')] : []) }
      case '/kitchen/menus/menu-1': return { data: menu(submitted ? 'submitted' : 'draft') }
      case '/meal-days/search': return { data: page([{ id: 'day-1', date, mealType: 'Lunch', isCancelled: false }, { id: 'cancelled', date, mealType: 'Snack', isCancelled: true }]) }
      case '/kitchen/recipes': return { data: page([{ recipeId: dish.recipeId, name: dish.recipeName, version: 2, versionId: dish.recipeVersionId }]) }
      case '/kitchen/recipes/recipe-1/versions': return { data: { name: dish.recipeName, items: [{ versionId: dish.recipeVersionId, version: 2, lines: [] }] } }
      default: throw new Error(url)
    }
  })
  const post = vi.spyOn(api, 'post').mockImplementation(async url => {
    if (url === '/kitchen/menus') { saved = true; return { data: { id: 'menu-1' } } }
    if (url === '/kitchen/menus/menu-1/submit') { submitted = true; return { data: {} } }
    throw new Error(url)
  })
  mount(<KitchenMenusPage isAdmin={false} />)
  fireEvent.click(screen.getByRole('button', { name: 'Tạo thực đơn' }))
  const editor = within(screen.getByRole('dialog'))
  const selector = await editor.findByLabelText('Phiên ăn')
  expect(editor.queryByRole('option', { name: /Bữa phụ/ })).toBeNull()
  fireEvent.change(selector, { target: { value: 'day-1' } })
  fireEvent.change(await editor.findByLabelText('Công thức'), { target: { value: 'recipe-1' } })
  fireEvent.click(await editor.findByRole('button', { name: 'Thêm món' }))
  fireEvent.change(editor.getByLabelText('Số suất dự kiến'), { target: { value: '30' } })
  fireEvent.click(editor.getByRole('button', { name: 'Lưu bản nháp' }))
  await waitFor(() => expect(post).toHaveBeenCalledWith('/kitchen/menus', { mealDayId: 'day-1', dishes: [{ recipeVersionId: 'recipe-v2', plannedPortions: 30 }], reason: null }))
  const start = await screen.findByRole('button', { name: 'Gửi duyệt' })
  fireEvent.click(start)
  fireEvent.click(screen.getByRole('button', { name: 'Gửi duyệt' }))
  await waitFor(() => expect(post).toHaveBeenCalledWith('/kitchen/menus/menu-1/submit', { expectedVersion: 2 }))
  await within(screen.getByRole('dialog')).findByText('Chờ duyệt')
  expect(screen.queryByRole('button', { name: /^Duyệt$/ })).toBeNull()
})

it('requires a rejection reason and publishes only an approved version through admin routes', async () => {
  let state: MenuState = 'submitted'
  vi.spyOn(api, 'get').mockImplementation(async url => ({ data: url.endsWith('menu-1') ? menu(state) : page([menu(state)]) }))
  const post = vi.spyOn(api, 'post').mockImplementation(async (_url, body) => {
    state = (body as { decision?: string }).decision === 'approve' ? 'approved' : 'published'
    return { data: {} }
  })
  mount(<KitchenMenusPage isAdmin />)
  fireEvent.click(await screen.findByRole('button', { name: 'Chi tiết' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Từ chối' }))
  const dialog = within(screen.getByRole('dialog'))
  expect(dialog.getByLabelText('Lý do từ chối').hasAttribute('required')).toBe(true)
  fireEvent.click(dialog.getByRole('button', { name: 'Từ chối' }))
  expect(post).not.toHaveBeenCalled()
  fireEvent.click(dialog.getByRole('button', { name: 'Quay lại' }))
  fireEvent.click(dialog.getByRole('button', { name: 'Duyệt' }))
  fireEvent.click(dialog.getByRole('button', { name: 'Duyệt' }))
  fireEvent.click(await dialog.findByRole('button', { name: 'Công bố' }))
  fireEvent.change(dialog.getByLabelText('Nhóm tuổi để đối chiếu (tùy chọn)'), { target: { value: '4–5 tuổi' } })
  fireEvent.click(dialog.getByRole('button', { name: 'Công bố' }))
  await dialog.findByText('Đã công bố')
  expect(post.mock.calls).toEqual([
    ['/admin/menus/menu-1/review', { expectedVersion: 2, decision: 'approve', reason: null }],
    ['/admin/menus/menu-1/publish', { expectedVersion: 2, ageGroup: '4–5 tuổi' }],
  ])
})

it('requires a fresh portion review after stale estimate and downloads the saved CSV', async () => {
  let token = 'old-snapshot'
  const result = { id: 'est-1', createdAt: timestamp, portions: [{ portions: 30 }], lines: [{ ingredientId: 'ingredient-1', ingredientName: 'Bí đỏ', quantity: 3000, unit: 'g', edibleGrams: 2500 }] }
  vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url.endsWith('/export')) return { data: new Blob(['csv']) }
    if (url.includes('/ingredient-estimates/')) return { data: result }
    if (url.endsWith('/diff')) return { data: { appliedTotal: 30, snapshotToken: token, classDiffs: [{ classId: 'A', className: 'A1', appliedCount: 30 }] } }
    return { data: url.endsWith('menu-1') ? menu('published') : page([menu('published')]) }
  })
  const post = vi.spyOn(api, 'post').mockRejectedValueOnce(conflict('STALE_SETTLEMENT')).mockResolvedValue({ data: result })
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
  vi.stubGlobal('URL', Object.assign(class extends URL {}, { createObjectURL: vi.fn(() => 'blob:test'), revokeObjectURL: vi.fn() }))
  try {
    mount(<KitchenMenusPage isAdmin={false} />)
    fireEvent.click(await screen.findByRole('button', { name: 'Chi tiết' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Dự trù' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'Đã đối chiếu số suất' }))
    fireEvent.click(screen.getByRole('button', { name: 'Tạo dự trù' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringContaining('Số suất đã thay đổi'), { toasterId: 'edit-modal' }))
    expect(screen.getByRole('checkbox', { name: 'Đã đối chiếu số suất' }).hasAttribute('disabled')).toBe(true)
    expect(screen.getByRole('button', { name: 'Tạo dự trù' }).hasAttribute('disabled')).toBe(true)
    token = 'new-snapshot'
    fireEvent.click(screen.getByRole('button', { name: 'Tải lại suất' }))
    await waitFor(() => expect(screen.getByRole('checkbox').hasAttribute('disabled')).toBe(false))
    fireEvent.click(screen.getByRole('checkbox'))
    fireEvent.click(screen.getByRole('button', { name: 'Tạo dự trù' }))
    expect(await screen.findByText('Bí đỏ')).toBeTruthy()
    expect(post).toHaveBeenLastCalledWith('/kitchen/ingredient-estimates', { menuVersionIds: ['menu-v2'], mealDayIds: ['day-1'], classIds: [], snapshotTokens: { 'day-1': 'new-snapshot' } })
    fireEvent.click(screen.getByRole('button', { name: 'Tải CSV' }))
    await waitFor(() => expect(api.get).toHaveBeenCalledWith('/kitchen/ingredient-estimates/est-1/export', { params: { format: 'csv' }, responseType: 'blob' }))
  } finally { vi.unstubAllGlobals() }
})

it('loads the selected linked child only and renders unknown nutrition without converting it to zero', async () => {
  const get = vi.spyOn(api, 'get').mockImplementation(async url => {
    if (url === '/parent/students') return { data: Object.values(responses) }
    return { data: { items: url.includes('child-2') ? [{ mealDayId: 'day-1', date, mealType: 'Lunch', dishes: [dish], nutrition: { kcal: 100, proteinG: 4, fatG: null, carbG: null } }] : [] } }
  })
  mount(<ParentMenusPage />)
  await screen.findByText('Chưa có thực đơn đã công bố trong khoảng ngày này.')
  fireEvent.change(screen.getByLabelText('Trẻ'), { target: { value: 'child-2' } })
  fireEvent.click(await screen.findByRole('button', { name: 'Chi tiết' }))
  const dialog = within(screen.getByRole('dialog'))
  expect(dialog.getByText('Canh bí đỏ')).toBeTruthy()
  expect(dialog.getAllByText('Chưa có')).toHaveLength(2)
  expect(get.mock.calls.map(([url]) => url)).toEqual(['/parent/students', '/parent/students/child-1/menus', '/parent/students/child-2/menus'])
})

it('uses the published nutrition even when a newer draft exists and displays recomputation separately', async () => {
  const published = menu('published').latest
  const current = { ...menu('published'), latest: { ...menu('draft').latest, version: 3, versionId: 'menu-v3' } }
  const frozen = { total: { kcal: 240, proteinG: 8, fatG: 6, carbG: 37 }, dishes: [], ageGroup: null, ageComparisonStatus: 'not_requested' }
  const get = vi.spyOn(api, 'get').mockImplementation(async (url, config) => {
    if (url === '/kitchen/menus') return { data: page([current]) }
    if (url.endsWith('/history')) return { data: { items: [published, current.latest] } }
    if (url.endsWith('/nutrition')) {
      const recomputed = { ...frozen, total: { ...frozen.total, kcal: 260 }, ageGroup: '4–5 tuổi', ageComparisonStatus: 'reference_missing' }
      const params = config?.params as { mode?: string } | undefined
      return { data: params?.mode === 'pinned' ? { payload: frozen } :
        { payload: recomputed, asPublished: frozen, deltaFromPublished: { kcal: 20, proteinG: 0, fatG: 0, carbG: 0 } } }
    }
    if (url.endsWith('/menu-1')) return { data: current }
    throw new Error(url)
  })
  mount(<KitchenMenusPage isAdmin={false} />)
  fireEvent.click(await screen.findByRole('button', { name: 'Chi tiết' }))
  fireEvent.click(await screen.findByRole('button', { name: 'Dinh dưỡng' }))
  expect(await screen.findByText('240 kcal')).toBeTruthy()
  expect(get).toHaveBeenCalledWith('/kitchen/menus/menu-1/nutrition', expect.objectContaining({ params: { version: 2, mode: 'pinned' } }))
  fireEvent.change(screen.getByLabelText('Nhóm tuổi (tùy chọn)'), { target: { value: '4–5 tuổi' } })
  fireEvent.change(screen.getByLabelText('Thời điểm áp dụng (giờ Việt Nam)'), { target: { value: '2026-11-27T07:00' } })
  fireEvent.click(screen.getByRole('button', { name: /^Tính lại$/ }))
  expect(await screen.findByText('260 kcal')).toBeTruthy()
  expect(screen.getByText('Chưa có khoảng tham chiếu cho nhóm tuổi này.')).toBeTruthy()
  expect(get).toHaveBeenCalledWith('/kitchen/menus/menu-1/nutrition', expect.objectContaining({ params: { version: 2, mode: 'recompute', ageGroup: '4–5 tuổi', validAt: '2026-11-27T07:00:00+07:00', knownAt: undefined } }))
})
