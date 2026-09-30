import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { toast } from 'sonner'

type Child = { studentId: string; fullName: string; className: string }
type Absence = { id: string; studentId: string; studentName: string; fromDate: string; toDate: string; reason: string; reportedAt: string; cancelledAt: string | null }
const localToday = () => new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).format(new Date())

export function AbsencesPage() {
  const queryClient = useQueryClient()
  const [studentId, setStudentId] = useState('')
  const [fromDate, setFromDate] = useState(localToday)
  const [toDate, setToDate] = useState(localToday)
  const [reason, setReason] = useState('')
  const children = useQuery({ queryKey: ['parent-students'], queryFn: async () => (await api.get<Child[]>('/parent/students')).data })
  const absences = useQuery({ queryKey: ['parent-absences'], queryFn: async () => (await api.get<Absence[]>('/parent/absences')).data })
  const report = useMutation({ mutationFn: () => api.post('/parent/absences', { studentId, fromDate, toDate, reason }),
    onSuccess: async () => { setReason(''); toast.success('Đã ghi báo vắng. Báo vắng sau giờ chốt không thay đổi bản suất đã gửi bếp.'); await queryClient.invalidateQueries({ queryKey: ['parent-absences'] }) },
    onError: error => toast.error(apiErrorMessage(error)) })
  const cancel = useMutation({ mutationFn: (id: string) => api.post(`/parent/absences/${id}/cancel`),
    onSuccess: async () => { toast.success('Đã hủy báo vắng. Bản suất đã chốt trước đó vẫn được giữ.'); await queryClient.invalidateQueries({ queryKey: ['parent-absences'] }) },
    onError: error => toast.error(apiErrorMessage(error)) })
  function submit(event: FormEvent) { event.preventDefault(); report.mutate() }

  return <><div className="eyebrow">PHỤ HUYNH</div><h1>Báo vắng</h1>
    <p className="lead">Trẻ được mặc định dự kiến ăn trong ngày học có bữa ăn. Chỉ cần báo khi trẻ vắng một ngày hoặc nhiều ngày.</p>
    <section className="panel"><h2>Tạo báo vắng</h2><form className="workflow-form" onSubmit={submit}>
      <div className="workflow-fields"><label className="field">Trẻ<select required value={studentId} onChange={e => setStudentId(e.target.value)}><option value="">Chọn trẻ</option>
        {children.data?.map(child => <option key={child.studentId} value={child.studentId}>{child.fullName} · {child.className}</option>)}</select></label>
        <label className="field">Từ ngày<input type="date" required min={localToday()} value={fromDate} onChange={e => { setFromDate(e.target.value); if (toDate < e.target.value) setToDate(e.target.value) }} /></label>
        <label className="field">Đến ngày<input type="date" required min={fromDate} value={toDate} onChange={e => setToDate(e.target.value)} /></label></div>
      <label className="field">Lý do<textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
      <button type="submit" className="button primary" disabled={report.isPending || !children.data?.length}>Gửi báo vắng</button>
      {!children.isPending && !children.data?.length && <p className="form-error">Tài khoản chưa được liên kết với trẻ. Liên hệ nhà trường.</p>}
    </form></section>
    <section className="panel workflow-lists"><h2>Báo vắng đã gửi</h2>{absences.isPending ? <p className="empty compact">Đang tải…</p> : absences.isError ? <p className="empty compact error">Không tải được báo vắng.</p> : !absences.data?.length ? <p className="empty compact">Chưa có báo vắng.</p> : absences.data.map(item =>
      <div className="entry workflow-entry" key={item.id}><div><strong>{item.studentName}</strong> · {item.fromDate} đến {item.toDate}<small>{item.reason} · {item.cancelledAt ? 'Đã hủy' : 'Đang hiệu lực'}</small></div>
        {!item.cancelledAt && <button type="button" className="button secondary" onClick={() => cancel.mutate(item.id)} disabled={cancel.isPending}>Hủy</button>}</div>)}</section>
  </>
}
