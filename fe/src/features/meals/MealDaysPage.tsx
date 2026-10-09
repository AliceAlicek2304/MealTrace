import { FilterPanel } from '../../components/FilterPanel'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { MealSettlementTables, type MealSettlement } from './MealSettlementTables'
import { schoolDateTime } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useEffect, useState } from 'react'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { QueryState } from '../../components/QueryState'

type MealDay = { id: string; date: string; mealType: string; publishedAt: string | null; isCancelled: boolean; cancellationReason: string | null; settledPortions: number | null; dishes: { id: string; name: string; recipeVersionId: string }[] }
type Detail = MealDay & { cutoffAt: string; settlements: MealSettlement[]; evidence: { id: string; kind: string; description: string; capturedAt: string; syncedAt: string; photoUrl: string | null }[] }
const formatDate = (value: string) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(`${value}T00:00:00`))
function mealStatusLabel(day: MealDay): string {
  if (day.isCancelled) return 'Đã hủy'
  if (day.settledPortions === null) return 'Chưa chốt'
  return 'Đã chốt'
}

export function MealDaysPage() {
  const [selected, setSelected] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [filterDate, setFilterDate] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const days = useQuery({ queryKey: ['meal-days', page, filterDate, status, searchTerm], enabled: !searchWaiting, queryFn: async () => (await api.get<{items: MealDay[]; total: number}>('/meal-days/search', { params: { page, date: filterDate || undefined, status: status || undefined, search: searchTerm || undefined } })).data })
  useEffect(() => {
    if (days.data) setPage(previous => Math.min(previous, Math.max(1, Math.ceil(days.data.total / 25))))
  }, [days.data])
  const detail = useQuery({ queryKey: ['meal-day', selected], queryFn: async () => (await api.get<Detail>(`/meal-days/${selected}`)).data, enabled: !!selected })
  const items = days.data?.items ?? []
  const record = detail.data
  return <>
    <div className="eyebrow">HỆ THỐNG QUẢN LÝ BỮA ĂN BÁN TRÚ</div><h1>Tổng quan ngày ăn</h1>
    <p className="lead">Theo dõi thực đơn, số suất đã chốt và hồ sơ thực hiện bữa ăn.</p>
    <section className="panel"><div className="panel-head"><h2>Danh sách ngày ăn</h2><p>{days.data?.total ?? 0} phiên ăn · phân trang toàn bộ lịch</p></div>
      <FilterPanel activeCount={[searchTerm, filterDate, status].filter(Boolean).length}><div className="list-toolbar"><label className="field">Tìm kiếm<input placeholder="Bữa ăn hoặc tên món" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Ngày ăn<input type="date" value={filterDate} onChange={e => { setFilterDate(e.target.value); setPage(1) }} /></label>
        <label className="field">Trạng thái<select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="SETTLED">Đã chốt</option><option value="PENDING">Chưa chốt</option><option value="CANCELLED">Đã hủy</option></select></label></div></FilterPanel>
      <SearchFeedback waiting={searchWaiting} fetching={days.isFetching} />
    <QueryState loading={days.isPending} error={days.isError} loadingMessage="Đang tải dữ liệu…" errorMessage="Không tải được ngày ăn."><div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Ngày</th><th scope="col">Bữa ăn</th><th scope="col">Thực đơn</th><th scope="col">Số suất</th><th scope="col">Trạng thái</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {items.map(day => <tr key={day.id}><td>{formatDate(day.date)}</td><td><strong>{day.mealType}</strong></td><td>{day.dishes.map(d => d.name).join(', ') || 'Chưa gắn món ăn'}</td><td>{day.settledPortions ?? '—'}</td><td>{mealStatusLabel(day)}<small>{day.publishedAt ? 'Đã công bố' : 'Chưa công bố'}</small></td><td><button type="button" className="button secondary" onClick={() => setSelected(day.id)}>Xem hồ sơ</button></td></tr>)}
        {!items.length && <tr><td colSpan={6} className="empty compact">{searchTerm || filterDate || status ? 'Không có ngày ăn phù hợp bộ lọc.' : 'Chưa có ngày ăn.'}</td></tr>}
      </tbody></ResponsiveTable></div></QueryState>
      <Pagination page={page} total={days.data?.total ?? 0} pageSize={25} busy={searchWaiting || days.isFetching} onChange={setPage} />
    </section>
    {selected && <Modal wide title={record ? `${record.mealType} · ${formatDate(record.date)}` : 'Hồ sơ ngày ăn'} onClose={() => setSelected(null)}>
      <QueryState loading={detail.isPending} error={detail.isError} loadingMessage="Đang tải…" errorMessage="Không tải được hồ sơ.">{record && <div className="workflow-form">
        {record.isCancelled && <p className="error">Phiên đã hủy: {record.cancellationReason}</p>}
        <p><strong>{record.settledPortions ?? 'Chưa chốt'} suất</strong> · Giờ chốt: {schoolDateTime(record.cutoffAt)}</p>
        <h3>Thực đơn dự kiến</h3><div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Món ăn</th></tr></thead><tbody>{record.dishes.map(d => <tr key={d.id}><td>{d.name}</td></tr>)}{!record.dishes.length && <tr><td>Chưa có món ăn.</td></tr>}</tbody></ResponsiveTable></div>
        <MealSettlementTables key={record.id} rows={record.settlements} />
        <h3>Minh chứng bữa ăn</h3><div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Loại / mô tả</th><th scope="col">Chụp / đồng bộ</th><th scope="col">Ảnh</th></tr></thead><tbody>{record.evidence.map(e => <tr key={e.id}><td><strong>{e.kind}</strong><small>{e.description}</small></td><td>{schoolDateTime(e.capturedAt)}<small>{schoolDateTime(e.syncedAt)}</small></td><td>{e.photoUrl ? <a href={e.photoUrl} target="_blank" rel="noreferrer">Xem ảnh</a> : '—'}</td></tr>)}{!record.evidence.length && <tr><td colSpan={3}>Chưa có minh chứng.</td></tr>}</tbody></ResponsiveTable></div>
      </div>}</QueryState>
    </Modal>}
  </>
}
