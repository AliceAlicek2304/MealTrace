import { FilterPanel } from '../../components/FilterPanel'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { schoolToday } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useEffect, useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { toast } from 'sonner'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'

type Child = { studentId: string; fullName: string; className: string; schoolYear: string; yearStartDate: string | null; yearEndDate: string | null }
type Year = { code: string; startDate: string; endDate: string }
type Absence = { id: string; studentId: string; studentName: string; fromDate: string; toDate: string; reason: string; reportedAt: string; cancelledAt: string | null; schoolYear: string | null }
const localToday = schoolToday
function periodEnd(start: string, period: 'week' | 'month') {
  const date = new Date(`${start}T00:00:00Z`)
  if (!Number.isFinite(date.getTime())) return ''
  if (period === 'week') date.setUTCDate(date.getUTCDate() + 6)
  else {
    const day = date.getUTCDate()
    date.setUTCDate(1)
    date.setUTCMonth(date.getUTCMonth() + 1)
    const last = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate()
    date.setUTCDate(Math.min(day, last) - 1)
  }
  return date.toISOString().slice(0, 10)
}

export function AbsencesPage() {
  const queryClient = useQueryClient()
  const [createOpen, setCreateOpen] = useState(false)
  const [cancelTarget, setCancelTarget] = useState<Absence | null>(null)
  const [search, setSearch] = useState('')
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [filterStudent, setFilterStudent] = useState('')
  const [filterStatus, setFilterStatus] = useState('')
  const [page, setPage] = useState(1)
  const [studentId, setStudentId] = useState('')
  const [fromDate, setFromDate] = useState(localToday)
  const [toDate, setToDate] = useState(localToday)
  const [reason, setReason] = useState('')
  const [editing, setEditing] = useState<Absence | null>(null)
  const [editFrom, setEditFrom] = useState('')
  const [editTo, setEditTo] = useState('')
  const [editReason, setEditReason] = useState('')
  const children = useQuery({ queryKey: ['parent-students'], queryFn: async () => (await api.get<Child[]>('/parent/students')).data })
  const years = useQuery({ queryKey: ['academic-years'], queryFn: async () => (await api.get<Year[]>('/academic-years')).data })
  const child = children.data?.find(x => x.studentId === studentId)
  const editYear = years.data?.find(x => x.code === editing?.schoolYear)
  const absences = useQuery({ queryKey: ['parent-absences', page, filterStudent, filterStatus, searchTerm], enabled: !searchWaiting, queryFn: async () => (await api.get<{items: Absence[]; total: number; students: {studentId: string; name: string}[]}>('/parent/absences/search', { params: { page, studentId: filterStudent || undefined, status: filterStatus || undefined, search: searchTerm || undefined } })).data })
  useEffect(() => {
    if (absences.data) setPage(previous => Math.min(previous, Math.max(1, Math.ceil(absences.data.total / 25))))
  }, [absences.data])
  const report = useMutation({ mutationFn: () => api.post('/parent/absences', { studentId, fromDate, toDate, reason }),
    onSuccess: async () => { setCreateOpen(false); setReason(''); toast.success('Đã đăng ký không ăn. Thay đổi sau giờ chốt không đổi số suất đã gửi bếp.'); await queryClient.invalidateQueries({ queryKey: ['parent-absences'] }) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  const cancel = useMutation({ mutationFn: (id: string) => api.post(`/parent/absences/${id}/cancel`),
    onSuccess: async () => { setCancelTarget(null); toast.success('Đã hủy báo vắng. Bản suất đã chốt trước đó vẫn được giữ.'); await queryClient.invalidateQueries({ queryKey: ['parent-absences'] }) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  const update = useMutation({ mutationFn: () => api.post(`/parent/absences/${editing!.id}/replace`, { studentId: editing!.studentId, fromDate: editFrom, toDate: editTo, reason: editReason }),
    onSuccess: async () => { setEditing(null); toast.success('Đã cập nhật khoảng không ăn; giữ lịch sử và số suất đã chốt.'); await queryClient.invalidateQueries({ queryKey: ['parent-absences'] }) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  function submit(event: FormEvent) { event.preventDefault(); if (!report.isPending) report.mutate() }
  function openEdit(item: Absence) { setEditing(item); setEditFrom(item.fromDate); setEditTo(item.toDate); setEditReason(item.reason) }
  const statusOf = (item: Absence) => item.cancelledAt ? 'CANCELLED' : item.toDate < localToday() ? 'EXPIRED' : item.fromDate > localToday() ? 'UPCOMING' : 'ACTIVE'
  const statusLabels: Record<string, string> = { CANCELLED: 'Đã hủy / thay thế', EXPIRED: 'Đã hết hạn', UPCOMING: 'Sắp áp dụng', ACTIVE: 'Đang hiệu lực' }
  const rows = absences.data?.items ?? []


  return <><div className="eyebrow">PHỤ HUYNH</div><h1>Báo vắng / Không ăn tại trường</h1>
    <p className="lead">Trẻ vẫn đi học có thể đăng ký không ăn tại trường theo tuần, tháng hoặc đến hết năm học. Khoảng ngày nằm trong niên khóa của trẻ; không tự kéo dài sang năm học mới. Việc đăng ký không thay đổi ghi danh học.</p>
    {children.data?.length === 0 && <p>Bạn chưa có trẻ được liên kết. <a href="#/links">Gửi yêu cầu liên kết trẻ</a> để nhà trường duyệt.</p>}
    {createOpen && <Modal title="Đăng ký không ăn" busy={report.isPending} onClose={() => setCreateOpen(false)}><form className="workflow-form" onSubmit={submit}>
      <div className="workflow-fields"><label className="field">Trẻ<select required value={studentId} onChange={e => { setStudentId(e.target.value); setFromDate(localToday()); setToDate(localToday()) }}><option value="">Chọn trẻ</option>
        {children.data?.map(child => <option key={child.studentId} value={child.studentId}>{child.fullName} · {child.className}</option>)}</select></label>
        <label className="field">Từ ngày<input type="date" required min={child?.yearStartDate && child.yearStartDate > localToday() ? child.yearStartDate : localToday()} max={child?.yearEndDate ?? undefined} value={fromDate} onChange={e => { setFromDate(e.target.value); if (toDate < e.target.value) setToDate(e.target.value) }} /></label>
        <label className="field">Đến ngày<input type="date" required min={fromDate} max={child?.yearEndDate ?? undefined} value={toDate} onChange={e => setToDate(e.target.value)} /></label></div>
      <div className="form-actions">{(['week', 'month'] as const).map((period, index) => <button type="button" className="button secondary" key={period} disabled={!child?.yearEndDate || !fromDate} onClick={() => { const end = periodEnd(fromDate, period); setToDate(end > child!.yearEndDate! ? child!.yearEndDate! : end) }}>{['1 tuần', '1 tháng'][index]}</button>)}
        <button type="button" className="button secondary" disabled={!child?.yearEndDate} onClick={() => setToDate(child!.yearEndDate!)}>Đến hết năm học</button></div>
      {child && <p className="form-help">Năm học {child.schoolYear}: {child.yearStartDate && child.yearEndDate ? `${child.yearStartDate} đến ${child.yearEndDate}` : 'Chưa thiết lập ngày bắt đầu/kết thúc. Liên hệ nhà trường.'}</p>}
      <p className="form-help">Tính cả ngày bắt đầu và kết thúc. Có thể sửa khoảng ngày hoặc hủy khi muốn ăn lại; phiên đã qua giờ chốt giữ nguyên.</p>
      <label className="field">Lý do<textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <button type="submit" className="button primary" disabled={report.isPending || !child?.yearEndDate}>Gửi đăng ký</button>
      {children.isError ? <p className="form-error">Không tải được danh sách trẻ. Đóng cửa sổ và thử lại.</p> : !children.isPending && !children.data?.length && <p className="form-error">Tài khoản chưa được liên kết với trẻ. Vào mục Liên kết trẻ để gửi yêu cầu.</p>}
    </form></Modal>}
    <section className="panel workflow-lists"><div className="panel-head"><h2>Đăng ký đã gửi</h2><button type="button" className="button primary" onClick={() => setCreateOpen(true)}>Đăng ký không ăn</button></div>
      <FilterPanel activeCount={[searchTerm, filterStudent, filterStatus].filter(Boolean).length}><div className="list-toolbar"><label className="field">Tìm kiếm<input placeholder="Tên trẻ hoặc lý do" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Trẻ<select value={filterStudent} onChange={e => { setFilterStudent(e.target.value); setPage(1) }}><option value="">Tất cả trẻ</option>{absences.data?.students.map(child => <option key={child.studentId} value={child.studentId}>{child.name}</option>)}</select></label>
        <label className="field">Trạng thái<select value={filterStatus} onChange={e => { setFilterStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option>{Object.entries(statusLabels).map(([id, label]) => <option key={id} value={id}>{label}</option>)}</select></label></div></FilterPanel>
      <SearchFeedback waiting={searchWaiting} fetching={absences.isFetching} />
    {absences.isPending ? <p className="empty compact">Đang tải…</p> : absences.isError ? <p className="empty compact error">Không tải được đăng ký.</p> : <div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Trẻ</th><th scope="col">Từ ngày</th><th scope="col">Đến ngày</th><th scope="col">Lý do</th><th scope="col">Trạng thái</th><th scope="col">Thao tác</th></tr></thead><tbody>{rows.map(item =>
      <tr key={item.id}><td><strong>{item.studentName}</strong></td><td>{item.fromDate}</td><td>{item.toDate}</td><td>{item.reason}</td><td>{statusLabels[statusOf(item)]}</td>
        <td>{!item.cancelledAt && item.toDate >= localToday() ? <div className="table-actions"><button type="button" className="button secondary" onClick={() => openEdit(item)} disabled={cancel.isPending}>Sửa</button><button type="button" className="button danger" onClick={() => setCancelTarget(item)} disabled={cancel.isPending}>Hủy / Ăn lại</button></div> : '—'}</td></tr>)}
      {!rows.length && <tr><td colSpan={6} className="empty compact">{searchTerm || filterStudent || filterStatus ? 'Không có đăng ký phù hợp bộ lọc.' : 'Chưa có đăng ký không ăn.'}</td></tr>}</tbody></ResponsiveTable></div>}
      <Pagination page={page} total={absences.data?.total ?? 0} pageSize={25} busy={searchWaiting || absences.isFetching} onChange={setPage} /></section>
    {cancelTarget && <Modal title={`Hủy đăng ký: ${cancelTarget.studentName}`} busy={cancel.isPending} onClose={() => setCancelTarget(null)}><div className="workflow-form"><p>Hủy khoảng không ăn từ {cancelTarget.fromDate} đến {cancelTarget.toDate}? Số suất đã qua giờ chốt giữ nguyên; lịch sử đăng ký vẫn được lưu.</p><div className="form-actions"><button type="button" className="button secondary" disabled={cancel.isPending} onClick={() => setCancelTarget(null)}>Quay lại</button><button type="button" className="button danger" disabled={cancel.isPending} onClick={() => cancel.mutate(cancelTarget.id)}>Xác nhận hủy</button></div></div></Modal>}
    {editing && <Modal title={`Cập nhật: ${editing.studentName}`} description="Bản cũ được giữ trong lịch sử. Số suất đã qua giờ chốt không thay đổi." busy={update.isPending} onClose={() => setEditing(null)}>
      <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!update.isPending) update.mutate() }}>
        <label className="field">Từ ngày<input type="date" required disabled={update.isPending} min={editing.fromDate < localToday() ? editing.fromDate : localToday()} value={editFrom} onChange={e => setEditFrom(e.target.value)} /></label>
        <p className="form-help">Nếu ngày bắt đầu đã qua, giữ nguyên ngày đó hoặc chọn một ngày từ hôm nay.</p>
        <p className="form-help">{editYear ? `Năm học ${editYear.code}: ${editYear.startDate} đến ${editYear.endDate}` : 'Nhà trường chưa thiết lập mốc năm học. Bạn vẫn có thể hủy đăng ký để ăn lại.'}</p>
        <label className="field">Đến ngày<input type="date" required disabled={update.isPending} min={editFrom > localToday() ? editFrom : localToday()} max={editYear?.endDate} value={editTo} onChange={e => setEditTo(e.target.value)} /></label>
        <label className="field">Lý do<textarea required maxLength={500} disabled={update.isPending} value={editReason} onChange={e => setEditReason(e.target.value)} /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={update.isPending} onClick={() => setEditing(null)}>Hủy</button><button type="submit" className="button primary" disabled={update.isPending || !editReason.trim() || !editYear}>Lưu cập nhật</button></div>
      </form>
    </Modal>}
  </>
}
