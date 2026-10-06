// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { PortionAmendments } from './PortionAmendments'
import { toast } from 'sonner'

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() }, Toaster: () => null }))
const child = (id: string, willEat = true) => ({ studentId: id, studentName: `Trẻ ${id}`, studentCode: `HS-${id}`, willEat, source: 'DEFAULT' })
const snapshot = { id: 'base', version: 1, count: 3, kitchenAdjustment: 0, students: [child('1'), child('2'), child('3')], decisions: [] }
const pending = { id: 'request', studentId: '1', studentName: 'Trẻ 1', studentCode: 'HS-1', baseSettlementId: 'base', baseVersion: 1,
  wasEating: true, willEat: false, quantity: 2, isQuantityOnly: false, students: [child('1'), child('2')], reason: 'Gia đình đón sớm',
  requestedByName: 'Giáo viên', requestedAt: '2026-10-06T01:00:00Z', status: 'PENDING', reviewReason: null, reviewedByName: null, reviewedAt: null }
function fixture() {
  return { className: 'M1', canRequest: true, original: snapshot, current: snapshot, hasOriginalSources: true, hasCompleteRoster: true,
    added: [], removed: [], candidates: [child('1'), child('2'), child('4', false)], candidateTotal: 26, items: [] as typeof pending[], total: 0 }
}
let data = fixture()
let cache: QueryClient

beforeEach(() => {
  data = fixture()
  cache = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, value: function (this: HTMLDialogElement) { this.setAttribute('open', '') } })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', { configurable: true, value: function (this: HTMLDialogElement) { this.removeAttribute('open') } })
  vi.spyOn(api, 'get').mockImplementation(async (url, config) => {
    if (url.endsWith('/request')) return { data: { before: snapshot, after: null } }
    const params = config?.params as { candidatePage?: number; q?: string } | undefined
    return { data: { ...data, candidates: params?.q ? [child('3')] : params?.candidatePage === 2 ? [child('3')] : data.candidates } }
  })
})
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
function show(roles = ['TEACHER']) {
  return render(<QueryClientProvider client={cache}><PortionAmendments mealId="meal" rooms={[{ classId: 'class', className: 'M1' }]} roles={roles} /></QueryClientProvider>)
}

describe('Batch portion amendment UI', () => {
  it('keeps selections across pages/search, selects only eligible children and removes one selected child', async () => {
    const user = userEvent.setup()
    show()
    await user.click(await screen.findByRole('button', { name: 'Tạo yêu cầu' }))
    await user.click(screen.getByRole('button', { name: 'Chọn trẻ phù hợp trong trang' }))
    expect((screen.getByRole('checkbox', { name: 'Chọn Trẻ 4' }) as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByText(/Đã chọn 2 trẻ/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Sau' }))
    await user.click(await screen.findByRole('checkbox', { name: 'Chọn Trẻ 3' }))
    expect(screen.getByText(/Đã chọn 3 trẻ/)).toBeTruthy()
    await user.type(screen.getByPlaceholderText('Tên hoặc mã trẻ'), 'Trẻ 3')
    await waitFor(() => expect(api.get).toHaveBeenLastCalledWith('/meal-days/meal/amendments', expect.objectContaining({ params: expect.objectContaining({ q: 'Trẻ 3', candidatePage: 1 }) })))
    expect(screen.getByText(/Đã chọn 3 trẻ/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Bỏ chọn Trẻ 1' }))
    expect(screen.getByText(/Đã chọn 2 trẻ/)).toBeTruthy()
    expect(screen.getByText(/Dự kiến gửi bếp: 3 → 1 suất/)).toBeTruthy()
  })

  it('submits one multi-child request then lets admin approve the entire request', async () => {
    const post = vi.spyOn(api, 'post').mockImplementation(async (url) => {
      data = url.endsWith('/review')
        ? { ...data, current: { ...snapshot, id: 'new', version: 2, count: 1, students: [child('3')] }, items: [{ ...pending, status: 'APPROVED' }] }
        : { ...data, items: [pending], total: 1 }
      return { data: {} }
    })
    const user = userEvent.setup()
    const teacher = show()
    await user.click(await screen.findByRole('button', { name: 'Tạo yêu cầu' }))
    await user.click(screen.getByRole('button', { name: 'Chọn trẻ phù hợp trong trang' }))
    await user.click(screen.getByRole('button', { name: 'Tạo phiếu giảm 2 suất' }))
    const modal = screen.getByRole('dialog', { name: 'Yêu cầu điều chỉnh suất' })
    expect(within(modal).getByText(/Gửi bếp: 3 → 1 suất/)).toBeTruthy()
    await user.type(within(modal).getByLabelText('Lý do'), 'Gia đình đón sớm')
    await user.click(within(modal).getByRole('button', { name: 'Gửi yêu cầu' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/meal-days/meal/amendments', { classId: 'class', studentIds: ['1', '2'], quantity: 2, baseSettlementId: 'base', willEat: false, reason: 'Gia đình đón sớm' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    teacher.unmount()
    show(['ADMIN'])
    await user.click(await screen.findByRole('button', { name: 'Xem và xử lý' }))
    await user.type(screen.getByLabelText('Lý do duyệt hoặc từ chối'), 'Đã đối chiếu')
    await waitFor(() => expect((screen.getByRole('button', { name: 'Duyệt và áp dụng' }) as HTMLButtonElement).disabled).toBe(false))
    await user.click(screen.getByRole('button', { name: 'Duyệt và áp dụng' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/meal-days/meal/amendments/request/review', { approve: true, reason: 'Đã đối chiếu' }))
    expect(await screen.findByText(/đang áp dụng 1 suất · bản 2/)).toBeTruthy()
  })

  it('blocks submitting a stale source while keeping the reason visible', async () => {
    const post = vi.spyOn(api, 'post')
    const user = userEvent.setup()
    show()
    await user.click(await screen.findByRole('button', { name: 'Tạo yêu cầu' }))
    await user.click(screen.getByRole('checkbox', { name: 'Chọn Trẻ 1' }))
    await user.click(screen.getByRole('button', { name: 'Tạo phiếu giảm 1 suất' }))
    await user.type(screen.getByLabelText('Lý do'), 'Đón sớm')
    act(() => cache.setQueryData(['portion-amendments', 'meal', 'class', '', 1, 1], { ...data, current: { ...snapshot, id: 'changed', version: 2 } }))
    expect(await screen.findByText(/Đóng cửa sổ và đối chiếu lại/)).toBeTruthy()
    expect((screen.getByRole('button', { name: 'Gửi yêu cầu' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.submit(screen.getByLabelText('Lý do').closest('form')!)
    expect(post).not.toHaveBeenCalled()
  })

  it('clearly separates a kitchen-only quantity and prevents reducing below zero', async () => {
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    const user = userEvent.setup()
    show()
    await user.click(await screen.findByRole('button', { name: 'Tạo yêu cầu' }))
    await user.selectOptions(screen.getByLabelText('Loại phiếu'), 'quantity')
    fireEvent.change(screen.getByLabelText('Số suất'), { target: { value: '4' } })
    expect((screen.getByRole('button', { name: 'Tạo phiếu giảm 4 suất' }) as HTMLButtonElement).disabled).toBe(true)
    fireEvent.change(screen.getByLabelText('Số suất'), { target: { value: '2' } })
    await user.click(screen.getByRole('button', { name: 'Tạo phiếu giảm 2 suất' }))
    const modal = screen.getByRole('dialog')
    expect(within(modal).getByText(/không thay đổi trạng thái ăn hoặc tiền ăn từng trẻ/)).toBeTruthy()
    await user.type(within(modal).getByLabelText('Lý do'), 'Bếp điều chỉnh')
    await user.click(within(modal).getByRole('button', { name: 'Gửi yêu cầu' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/meal-days/meal/amendments', expect.objectContaining({ studentIds: [], quantity: 2 })))
  })

  it('keeps the form and reason on server conflict, refreshes the source and shows an error toast', async () => {
    vi.spyOn(api, 'post').mockImplementation(async () => {
      data = { ...data, current: { ...snapshot, id: 'newer', version: 2 } }
      throw { isAxiosError: true, response: { status: 409, data: { message: 'Bản nguồn đã thay đổi.' } } }
    })
    const user = userEvent.setup()
    show()
    await user.click(await screen.findByRole('button', { name: 'Tạo yêu cầu' }))
    await user.click(screen.getByRole('checkbox', { name: 'Chọn Trẻ 1' }))
    await user.click(screen.getByRole('button', { name: 'Tạo phiếu giảm 1 suất' }))
    await user.type(screen.getByLabelText('Lý do'), 'Đón sớm')
    await user.click(screen.getByRole('button', { name: 'Gửi yêu cầu' }))
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Bản nguồn đã thay đổi.', { toasterId: 'edit-modal' }))
    expect(await screen.findByText(/Đóng cửa sổ và đối chiếu lại/)).toBeTruthy()
    expect((screen.getByLabelText('Lý do') as HTMLTextAreaElement).value).toBe('Đón sớm')
    expect((screen.getByRole('button', { name: 'Gửi yêu cầu' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
