import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { SchoolYearPicker } from '../../components/SchoolYearPicker'

type CalendarDay = { date: string; isOpen: boolean; mealTypes: string[]; isException: boolean; reason: string | null; locked: boolean;
  sessions: { id: string; mealType: string; isCancelled: boolean; isSettled: boolean }[] }
type Calendar = { schoolYear: string; startDate: string; endDate: string; from: string; to: string; revision: number; weekdays: number[]; mealTypes: string[]; days: CalendarDay[] }
type Range = { from: string; to: string }
type GenerateRequest = Range & { expectedRevision: number; previewToken?: string }
type Plan = { date: string; mealType: string; action: string }
type Preview = { items: Plan[]; previewToken: string; createCount: number; restoreCount: number; existingCount: number }
type Edit = { code: string; revision: number; kind: 'schedule' | 'day'; date?: string }
type Audit = { id: string; date: string | null; kind: string; reason: string; actorName: string; recordedAt: string }
const weekdays = ['Chủ nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy']
const actionLabel: Record<string, string> = { CREATE: 'Tạo mới', RESTORE: 'Khôi phục phiên', EXISTS: 'Đã có — bỏ qua', CLOSED: 'Ngày nghỉ — bỏ qua', LOCKED: 'Qua giờ chốt / đã khóa — bỏ qua', OTHER_YEAR: 'Phiên thuộc năm khác — bỏ qua' }
const auditLabel: Record<string, string> = { SCHEDULE: 'Lịch tuần', OPEN: 'Ngày có ăn', CLOSED: 'Ngày nghỉ', DEFAULT: 'Trở về lịch tuần', GENERATE: 'Tạo phiên hàng loạt' }
const splitTypes = (text: string) => text.split('\n').map(x => x.trim()).filter(Boolean)
function rangeEnd(from: string, period: 'week' | 'month') {
  const value = new Date(`${from}T00:00:00Z`)
  if (!Number.isFinite(value.getTime())) return from
  if (period === 'week') value.setUTCDate(value.getUTCDate() + 6)
  else { const day = value.getUTCDate(); value.setUTCDate(1); value.setUTCMonth(value.getUTCMonth() + 1);
    const last = new Date(Date.UTC(value.getUTCFullYear(), value.getUTCMonth() + 1, 0)).getUTCDate(); value.setUTCDate(Math.min(day, last) - 1) }
  return value.toISOString().slice(0, 10)
}

export function MealCalendarPage() {
  const cache = useQueryClient()
  const [code, setCode] = useState('')
  const [range, setRange] = useState<Range | null>(null)
  const [dayPage, setDayPage] = useState(1)
  const [edit, setEdit] = useState<Edit | null>(null)
  const [checkedDays, setCheckedDays] = useState<number[]>([])
  const [typesText, setTypesText] = useState('')
  const [mode, setMode] = useState('OPEN')
  const [reason, setReason] = useState('')
  const [previewDialog, setPreviewDialog] = useState<{ code: string; input: GenerateRequest; data: Preview } | null>(null)
  const [previewPage, setPreviewPage] = useState(1)
  const [historyOpen, setHistoryOpen] = useState(false)
  const calendar = useQuery({ queryKey: ['meal-calendar', code, range?.from, range?.to], enabled: !!code,
    queryFn: async () => (await api.get<Calendar>(`/admin/meal-calendar/${encodeURIComponent(code)}`, { params: range ?? {} })).data })
  const history = useQuery({ queryKey: ['calendar-history', code], enabled: !!code && historyOpen,
    queryFn: async () => (await api.get<Audit[]>(`/admin/meal-calendar/${encodeURIComponent(code)}/history`)).data })
  const data = calendar.data
  const currentRange = range ?? (data ? { from: data.from, to: data.to } : { from: '', to: '' })
  async function refresh() {
    await Promise.all(['meal-calendar', 'calendar-history', 'workflow-days', 'meal-days', 'portions', 'meal-decisions', 'meal-day'].map(key => cache.invalidateQueries({ queryKey: [key] })))
  }
  const save = useMutation({ mutationFn: async () => {
    if (edit!.kind === 'schedule') await api.put(`/admin/meal-calendar/${encodeURIComponent(edit!.code)}/schedule`, { weekdays: checkedDays, mealTypes: splitTypes(typesText), expectedRevision: edit!.revision, reason })
    else await api.put(`/admin/meal-calendar/${encodeURIComponent(edit!.code)}/days/${edit!.date}`, { mode, mealTypes: mode === 'OPEN' ? splitTypes(typesText) : [], expectedRevision: edit!.revision, reason })
  },
    onSuccess: async () => { const wasOpen = edit?.kind === 'day' && mode !== 'CLOSED'; setEdit(null); toast.success(wasOpen ? 'Đã cập nhật ngày ăn. Xem trước để tạo những phiên còn thiếu.' : 'Đã cập nhật lịch; giữ lịch sử các phiên.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await cache.invalidateQueries({ queryKey: ['meal-calendar'] }) } })
  const preview = useMutation({ mutationFn: async (request: { code: string; input: GenerateRequest }) => ({ ...request,
    data: (await api.post<Preview>(`/admin/meal-calendar/${encodeURIComponent(request.code)}/preview`, request.input)).data }),
    onSuccess: result => { setPreviewDialog(result); setPreviewPage(1) }, onError: async error => { toast.error(apiErrorMessage(error)); await cache.invalidateQueries({ queryKey: ['meal-calendar'] }) } })
  const generate = useMutation({ mutationFn: async () => (await api.post<{ created: number; restored: number; existing: number }>(`/admin/meal-calendar/${encodeURIComponent(previewDialog!.code)}/generate`,
    { ...previewDialog!.input, previewToken: previewDialog!.data.previewToken })).data,
    onSuccess: async result => { setPreviewDialog(null); toast.success(`Đã tạo ${result.created} phiên, khôi phục ${result.restored}, bỏ qua ${result.existing} phiên đã có.`); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await cache.invalidateQueries({ queryKey: ['meal-calendar'] }) } })
  function openSchedule() {
    if (!data) return
    setEdit({ code, revision: data.revision, kind: 'schedule' }); setCheckedDays(data.revision ? data.weekdays : [1, 2, 3, 4, 5]); setTypesText(data.mealTypes.length ? data.mealTypes.join('\n') : 'Bữa trưa'); setReason('')
  }
  function openDay(day: CalendarDay) {
    if (!data) return
    setEdit({ code, revision: data.revision, kind: 'day', date: day.date }); setMode(day.isOpen ? 'OPEN' : 'CLOSED'); setTypesText((day.mealTypes.length ? day.mealTypes : data.mealTypes).join('\n')); setReason('')
  }
  function setPeriod(period: 'week' | 'month' | 'year') {
    if (!data) return
    const from = period === 'year' ? data.startDate : currentRange.from
    const to = period === 'year' ? data.endDate : rangeEnd(from, period)
    setRange({ from, to: to > data.endDate ? data.endDate : to }); setDayPage(1)
  }
  return <><div className="eyebrow">LỊCH VẬN HÀNH</div><h1>Lịch bữa ăn</h1><p className="lead">Thiết lập lịch tuần một lần, tạo phiên theo tuần/tháng/năm học và xử lý ngày nghỉ hoặc học bù. Tạo phiên trước không chốt số suất trước.</p>
    <section className="panel workflow-form"><SchoolYearPicker configuredOnly value={code} onChange={value => { setCode(value); setRange(null); setDayPage(1) }} /></section>
    {code && (calendar.isPending ? <p>Đang tải lịch…</p> : calendar.isError ? <p className="error">{apiErrorMessage(calendar.error)}</p> : data && <>
      <section className="panel"><div className="panel-head"><div><h2>Lịch tuần · {code}</h2><p>{data.revision ? `${data.weekdays.map(x => weekdays[x]).join(', ') || 'Chỉ mở ngày đặc biệt'} · ${data.mealTypes.join(', ')}` : 'Chưa thiết lập. Các lựa chọn gợi ý chỉ áp dụng sau khi bạn lưu.'}</p></div>
        <button type="button" className="button primary" onClick={openSchedule}>{data.revision ? 'Sửa lịch tuần' : 'Thiết lập lịch tuần'}</button></div>
        <p className="empty compact">Năm học: {data.startDate} đến {data.endDate}. Thay đổi lịch chỉ xử lý phiên còn trước giờ chốt. Phiên đã chốt/công bố không được hủy; không xóa lịch sử.</p></section>
      <section className="panel"><div className="panel-head"><div><h2>Tạo lịch hàng loạt</h2><p>Chọn khoảng ngày, xem trước rồi xác nhận. Ngày nghỉ, phiên đã có hoặc ngày đã qua giờ chốt sẽ được bỏ qua.</p></div></div>
        <div className="workflow-form"><div className="workflow-fields"><label className="field">Từ ngày<input type="date" required min={data.startDate} max={data.endDate} value={currentRange.from} onChange={e => { const value = e.target.value; if (value < data.startDate || value > data.endDate) return; setRange({ from: value, to: value > currentRange.to ? value : currentRange.to }); setDayPage(1) }} /></label>
          <label className="field">Đến ngày<input type="date" required min={currentRange.from} max={data.endDate} value={currentRange.to} onChange={e => { const value = e.target.value; if (value < currentRange.from || value > data.endDate) return; setRange({ from: currentRange.from, to: value }); setDayPage(1) }} /></label></div>
          <div className="form-actions"><button type="button" className="button secondary" onClick={() => setPeriod('week')}>1 tuần</button><button type="button" className="button secondary" onClick={() => setPeriod('month')}>1 tháng</button><button type="button" className="button secondary" onClick={() => setPeriod('year')}>Cả năm học</button>
            <button type="button" className="button primary" disabled={!data.revision || calendar.isFetching || preview.isPending || !currentRange.from || !currentRange.to || currentRange.to < currentRange.from} onClick={() => preview.mutate({ code, input: { ...currentRange, expectedRevision: data.revision } })}>Xem trước tạo phiên</button></div>
        </div></section>
      <section className="panel workflow-lists"><div className="panel-head"><div><h2>Ngày nghỉ và ngày ăn đặc biệt</h2><p>Đổi ngày bằng modal. Mở ngày học bù rồi xem trước tạo phiên còn thiếu. “Theo lịch tuần” bỏ thiết lập riêng của ngày.</p></div><button type="button" className="button secondary" onClick={() => setHistoryOpen(true)}>Lịch sử thay đổi</button></div>
        <div className="calendar-grid">{data.days.slice((dayPage - 1) * 31, dayPage * 31).map(day => <article className={`calendar-day ${day.isOpen ? 'calendar-open' : 'calendar-closed'}`} key={day.date}>
          <strong>{day.date} · {weekdays[new Date(`${day.date}T00:00:00Z`).getUTCDay()]}</strong><small>{day.isOpen ? day.mealTypes.join(', ') : 'Không tổ chức ăn'}{day.isException ? ' · Ngày đặc biệt' : ''}</small>
          {day.reason && <small>{day.reason}</small>}{day.sessions.map(session => <small key={session.id}>{session.mealType}: {session.isCancelled ? 'Đã hủy' : session.isSettled ? 'Đã chốt' : 'Đã tạo phiên'}</small>)}
          <button type="button" className="button secondary" disabled={!data.revision || day.locked || calendar.isFetching} onClick={() => openDay(day)}>{day.locked ? 'Đã khóa ngày' : 'Sửa ngày'}</button>
        </article>)}</div><Pagination page={dayPage} total={data.days.length} pageSize={31} busy={calendar.isFetching} onChange={setDayPage} /></section>
    </>)}
    {edit && <Modal title={edit.kind === 'schedule' ? 'Thiết lập lịch tuần' : `Chỉnh ngày ${edit.date}`} description="Bắt buộc lý do. Phiên đã chốt hoặc qua giờ chốt giữ nguyên; lỗi lưu giữ dữ liệu bạn nhập." busy={save.isPending} onClose={() => setEdit(null)}>
      <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!save.isPending) save.mutate() }}>
        {edit.kind === 'schedule' ? <fieldset className="calendar-weekdays"><legend>Các thứ có tổ chức ăn</legend>{[1, 2, 3, 4, 5, 6, 0].map(value => <label key={value}><input type="checkbox" disabled={save.isPending} checked={checkedDays.includes(value)} onChange={e => setCheckedDays(previous => e.target.checked ? [...previous, value] : previous.filter(x => x !== value))} />{weekdays[value]}</label>)}</fieldset> :
          <label className="field">Lịch ngày<select disabled={save.isPending} value={mode} onChange={e => setMode(e.target.value)}><option value="CLOSED">Ngày nghỉ — không tổ chức ăn</option><option value="OPEN">Có tổ chức ăn / học bù</option><option value="DEFAULT">Trở về lịch tuần</option></select></label>}
        {(edit.kind === 'schedule' || mode === 'OPEN') && <label className="field">Các phiên phục vụ (mỗi dòng một phiên, tối đa 6)<textarea required maxLength={400} rows={4} disabled={save.isPending} value={typesText} onChange={e => setTypesText(e.target.value)} placeholder={'Bữa trưa\nBữa phụ'} /></label>}
        <label className="field">Lý do<textarea required maxLength={500} disabled={save.isPending} value={reason} onChange={e => setReason(e.target.value)} /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={save.isPending} onClick={() => setEdit(null)}>Hủy</button><button type="submit" className="button primary" disabled={save.isPending || !reason.trim()}>Lưu lịch</button></div>
      </form></Modal>}
    {previewDialog && <Modal wide title={`Xem trước · ${previewDialog.code}`} description={`${previewDialog.input.from} đến ${previewDialog.input.to}. Chưa tạo dữ liệu trước khi xác nhận.`} busy={generate.isPending} onClose={() => setPreviewDialog(null)}>
      <div className="workflow-form"><p><strong>{previewDialog.data.createCount} phiên mới · {previewDialog.data.restoreCount} khôi phục · {previewDialog.data.existingCount} phiên đã có</strong></p>
        {previewDialog.data.items.slice((previewPage - 1) * 25, previewPage * 25).map(item => <div className="entry" key={`${item.date}-${item.mealType}`}><strong>{item.date} {item.mealType && `· ${item.mealType}`}</strong><small>{actionLabel[item.action] ?? item.action}</small></div>)}
        <Pagination page={previewPage} total={previewDialog.data.items.length} pageSize={25} busy={generate.isPending} onChange={setPreviewPage} />
        <div className="form-actions"><button type="button" className="button secondary" disabled={generate.isPending} onClick={() => setPreviewDialog(null)}>Đóng</button><button type="button" className="button primary" disabled={generate.isPending || !(previewDialog.data.createCount + previewDialog.data.restoreCount)} onClick={() => generate.mutate()}>Xác nhận tạo lịch</button></div>
      </div></Modal>}
    {historyOpen && <Modal title="Lịch sử thay đổi lịch bữa ăn" description="50 thay đổi gần nhất của năm học." onClose={() => setHistoryOpen(false)}><div className="workflow-form">
      {history.isPending ? <p>Đang tải…</p> : history.isError ? <p className="error">{apiErrorMessage(history.error)}</p> : !history.data?.length ? <p>Chưa có thay đổi.</p> : history.data.map(item => <div className="entry" key={item.id}><strong>{auditLabel[item.kind]} {item.date}</strong><small>{item.actorName} · {new Date(item.recordedAt).toLocaleString('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh' })}</small><p>{item.reason}</p></div>)}
    </div></Modal>}
  </>
}
