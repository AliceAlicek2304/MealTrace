import { useState, type ReactNode } from 'react'
import { useQuery, type QueryKey } from '@tanstack/react-query'
import { isAxiosError } from 'axios'
import { toast } from 'sonner'
import { Pagination } from '../../components/Pagination'
import { QueryState } from '../../components/QueryState'
import { apiErrorMessage } from '../../lib/api'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { pageSize, type DateRange, type PageResult } from './operationsApi'

const messages: Record<string, string> = {
  VALIDATION_ERROR: 'Thông tin chưa hợp lệ. Vui lòng kiểm tra các trường đã nhập.',
  MENU_ALREADY_EXISTS: 'Phiên ăn này đã có thực đơn. Hãy mở thực đơn để tạo phiên bản mới.',
  MENU_STATE_INVALID: 'Thực đơn đã đổi trạng thái. Hãy tải lại và kiểm tra trước khi thao tác.',
  MENU_NOT_PUBLISHED: 'Cần có thực đơn đã công bố để thực hiện thao tác này.',
  MEAL_DAY_CANCELLED: 'Phiên ăn này đã bị hủy.',
  STALE_SETTLEMENT: 'Số suất đã thay đổi. Hãy tải lại bản suất và kiểm tra trước khi tạo dự trù.',
  NUTRIENT_DATA_INCOMPLETE: 'Công thức còn thiếu dữ liệu dinh dưỡng. Hãy cập nhật trước khi công bố.',
  LEGACY_CONVERSION_UNVERIFIED: 'Công thức có dòng quy đổi cũ chưa được xác minh.',
  UNIT_CONVERSION_MISSING: 'Thiếu dữ liệu quy đổi đơn vị trong công thức.',
  AGE_GROUP_REFERENCE_MISSING: 'Chưa có dữ liệu tham chiếu cho nhóm tuổi này.',
  REFERENCE_BASIS_MISMATCH: 'Khoảng tham chiếu không phù hợp với loại bữa ăn này.',
}
export function operationError(error: unknown) {
  if (!isAxiosError(error)) return apiErrorMessage(error)
  const code: unknown = error.response?.data?.code
  if (typeof code === 'string' && Object.hasOwn(messages, code)) return messages[code]
  if (error.response?.status === 409) return 'Dữ liệu đã thay đổi. Hãy mở lại bản mới nhất trước khi lưu.'
  if (error.response?.status === 400 || error.response?.status === 422) return messages.VALIDATION_ERROR
  if (error.response?.status === 404) return 'Không tìm thấy dữ liệu này.'
  return apiErrorMessage(error)
}
export function operationToast(error: unknown) { toast.error(operationError(error), { toasterId: 'edit-modal' }) }
export function optionalText(value: string) { return value.trim() || null }
export function fieldText(form: FormData, key: string) { return String(form.get(key) ?? '').trim() }
export function mealLabel(value: string) {
  switch (value.toLowerCase()) {
    case 'breakfast': return 'Bữa sáng'
    case 'lunch': return 'Bữa trưa'
    case 'snack': return 'Bữa phụ'
    default: return value
  }
}
export function dateLabel(value: string) {
  const [year, month, day] = value.split('-')
  return day ? `${day}/${month}/${year}` : '—'
}
const number = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 4 })
export function numberLabel(value: number | null) { return value === null ? 'Chưa có' : number.format(value) }

export function DataState({ query, children }: Readonly<{
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => Promise<unknown> }
  children: ReactNode
}>) {
  return <QueryState loadingMessage="Đang tải…" loading={query.isPending} error={query.isError}
    errorMessage={operationError(query.error)} onRetry={() => void query.refetch()}>{children}</QueryState>
}

export function DateFields({ value, onChange, required = false }: Readonly<{
  value: DateRange; onChange: (range: DateRange) => void; required?: boolean
}>) {
  return <div className="form-grid">
    <label className="field"><span>Từ ngày</span><input type="date" required={required} value={value.from} max={value.to || undefined} onChange={e => onChange({ ...value, from: e.target.value })} /></label>
    <label className="field"><span>Đến ngày</span><input type="date" required={required} value={value.to} min={value.from || undefined} onChange={e => onChange({ ...value, to: e.target.value })} /></label>
  </div>
}

type Choice = { id: string; label: string }
export function CatalogPicker<T>({ label, prefix, value, options, toChoice, onChange }: Readonly<{
  label: string; prefix: QueryKey; value: Choice | null
  options: (search: string, page: number, signal: AbortSignal) => Promise<PageResult<T>>
  toChoice: (record: T) => Choice; onChange: (record: T | null) => void
}>) {
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())
  const [page, setPage] = useState(1)
  const query = useQuery({
    queryKey: [...prefix, term, page],
    queryFn: ({ signal }) => options(term, page, signal),
  })
  const items = query.data?.items ?? []
  const choices = items.map(toChoice)
  return <div className="operations-picker">
    <label className="field"><span>Tìm {label.toLowerCase()}</span><input value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
    <DataState query={query}>
      <label className="field"><span>{label}</span><select value={value?.id ?? ''} disabled={search.trim() !== term} onChange={e => {
        const id = e.target.value
        onChange(items.find(item => toChoice(item).id === id) ?? null)
      }}>
        <option value="">Chọn {label.toLowerCase()}</option>
        {value && !choices.some(choice => choice.id === value.id) && <option value={value.id}>{value.label}</option>}
        {choices.map(choice => <option key={choice.id} value={choice.id}>{choice.label}</option>)}
      </select></label>
      {!choices.length && <small>Không tìm thấy dữ liệu phù hợp.</small>}
      {(query.data?.total ?? 0) > pageSize && <Pagination page={page} pageSize={pageSize} total={query.data!.total} busy={query.isFetching} onChange={setPage} />}
    </DataState>
  </div>
}
