import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useState } from 'react'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'

type MealDay = { id: string; date: string; mealType: string; publishedAt: string | null; isCancelled: boolean; cancellationReason: string | null; settledPortions: number | null; dishes: { id: string; name: string; recipeVersionId: string }[] }
type Detail = MealDay & { cutoffAt: string; settlements: { id: string; classId: string | null; className: string | null; count: number; settledAt: string }[]; evidence: { id: string; kind: string; description: string; capturedAt: string; syncedAt: string; photoUrl: string | null }[] }
const formatDate = (value: string) => new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(new Date(`${value}T00:00:00`))

export function MealDaysPage() {
  const [selected, setSelected] = useState<string | null>(null)
  const [search, setSearch] = useState('')
  const [filterDate, setFilterDate] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const days = useQuery({ queryKey: ['meal-days'], queryFn: async () => (await api.get<MealDay[]>('/meal-days')).data })
  const detail = useQuery({ queryKey: ['meal-day', selected], queryFn: async () => (await api.get<Detail>(`/meal-days/${selected}`)).data, enabled: !!selected })
  const items = (days.data ?? []).filter(day => (!filterDate || day.date === filterDate) &&
    (!status || (status === 'CANCELLED' ? day.isCancelled : status === 'SETTLED' ? !day.isCancelled && day.settledPortions !== null : !day.isCancelled && day.settledPortions === null)) &&
    `${day.mealType} ${day.dishes.map(d => d.name).join(' ')}`.toLocaleLowerCase('vi-VN').includes(search.trim().toLocaleLowerCase('vi-VN')))
  const record = detail.data
  const visiblePage = Math.min(page, Math.max(1, Math.ceil(items.length / 25)))
  return <>
    <div className="eyebrow">HỆ THỐNG QUẢN LÝ BỮA ĂN BÁN TRÚ</div><h1>Tổng quan ngày ăn</h1>
    <p className="lead">Theo dõi thực đơn, số suất đã chốt và hồ sơ thực hiện bữa ăn.</p>
    <section className="panel"><div className="panel-head"><h2>Danh sách ngày ăn</h2><p>Tối đa 100 bản ghi gần nhất</p></div>
      <div className="list-toolbar"><label className="field">Tìm kiếm<input placeholder="Bữa ăn hoặc tên món" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Ngày ăn<input type="date" value={filterDate} onChange={e => { setFilterDate(e.target.value); setPage(1) }} /></label>
        <label className="field">Trạng thái<select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="SETTLED">Đã chốt</option><option value="PENDING">Chưa chốt</option><option value="CANCELLED">Đã hủy</option></select></label></div>
      {days.isPending ? <p className="empty compact">Đang tải dữ liệu…</p> : days.isError ? <p className="empty compact error">Không tải được ngày ăn.</p> : <div className="table-wrap"><table><thead><tr><th scope="col">Ngày</th><th scope="col">Bữa ăn</th><th scope="col">Thực đơn</th><th scope="col">Số suất</th><th scope="col">Trạng thái</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {items.slice((visiblePage - 1) * 25, visiblePage * 25).map(day => <tr key={day.id}><td>{formatDate(day.date)}</td><td><strong>{day.mealType}</strong></td><td>{day.dishes.map(d => d.name).join(', ') || 'Chưa gắn món ăn'}</td><td>{day.settledPortions ?? '—'}</td><td>{day.isCancelled ? 'Đã hủy' : day.settledPortions === null ? 'Chưa chốt' : 'Đã chốt'}<small>{day.publishedAt ? 'Đã công bố' : 'Chưa công bố'}</small></td><td><button type="button" className="button secondary" onClick={() => setSelected(day.id)}>Xem hồ sơ</button></td></tr>)}
        {!items.length && <tr><td colSpan={6} className="empty compact">Không có ngày ăn phù hợp.</td></tr>}
      </tbody></table></div>}
      <Pagination page={visiblePage} total={items.length} pageSize={25} busy={days.isFetching} onChange={setPage} />
    </section>
    {selected && <Modal wide title={record ? `${record.mealType} · ${formatDate(record.date)}` : 'Hồ sơ ngày ăn'} onClose={() => setSelected(null)}>
      {detail.isPending ? <p className="empty compact">Đang tải…</p> : detail.isError ? <p className="empty compact error">Không tải được hồ sơ.</p> : record && <div className="workflow-form">
        {record.isCancelled && <p className="error">Phiên đã hủy: {record.cancellationReason}</p>}
        <p><strong>{record.settledPortions ?? 'Chưa chốt'} suất</strong> · Giờ chốt: {new Date(record.cutoffAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</p>
        <h3>Thực đơn dự kiến</h3><div className="table-wrap"><table><thead><tr><th scope="col">Món ăn</th></tr></thead><tbody>{record.dishes.map(d => <tr key={d.id}><td>{d.name}</td></tr>)}{!record.dishes.length && <tr><td>Chưa có món ăn.</td></tr>}</tbody></table></div>
        <h3>Số suất theo lớp</h3><div className="table-wrap"><table><thead><tr><th scope="col">Lớp</th><th scope="col">Số suất</th><th scope="col">Thời điểm chốt</th></tr></thead><tbody>{record.settlements.map(row => <tr key={row.id}><td>{row.className ?? 'Chưa có lớp'}</td><td>{row.count}</td><td>{new Date(row.settledAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</td></tr>)}{!record.settlements.length && <tr><td colSpan={3}>Chưa có bản chốt.</td></tr>}</tbody></table></div>
        <h3>Minh chứng bữa ăn</h3><div className="table-wrap"><table><thead><tr><th scope="col">Loại / mô tả</th><th scope="col">Chụp / đồng bộ</th><th scope="col">Ảnh</th></tr></thead><tbody>{record.evidence.map(e => <tr key={e.id}><td><strong>{e.kind}</strong><small>{e.description}</small></td><td>{new Date(e.capturedAt).toLocaleString('vi-VN')}<small>{new Date(e.syncedAt).toLocaleString('vi-VN')}</small></td><td>{e.photoUrl ? <a href={e.photoUrl} target="_blank" rel="noreferrer">Xem ảnh</a> : '—'}</td></tr>)}{!record.evidence.length && <tr><td colSpan={3}>Chưa có minh chứng.</td></tr>}</tbody></table></div>
      </div>}
    </Modal>}
  </>
}
