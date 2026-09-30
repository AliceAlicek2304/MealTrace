import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { ClassPicker } from '../../components/ClassPicker'
import { Pagination } from '../../components/Pagination'

type Page<T> = { items: T[]; total: number; earliestChangeDate?: string }
type Room = { id: string; name: string; schoolYear: string; studentCount: number }
type Student = { id: string; studentCode: string; fullName: string; revision: number; isActive: boolean; className: string;
  parents: { id: string; fullName: string; email: string | null; phoneNumber: string | null }[] }
type Enrollment = { id: string; className: string; schoolYear: string; startDate: string; endDate: string | null; reason: string; endReason: string | null }
type Editor = { kind: 'class'; room: Room } | { kind: 'student' | 'enrollment'; student: Student }

export function StudentDirectory({ onLink }: { onLink: (student: { id: string; fullName: string }) => void }) {
  const cache = useQueryClient()
  const [classSearch, setClassSearch] = useState('')
  const [classPage, setClassPage] = useState(1)
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [classId, setClassId] = useState('')
  const [status, setStatus] = useState('')
  const [editor, setEditor] = useState<Editor | null>(null)
  const [name, setName] = useState('')
  const [targetClass, setTargetClass] = useState('')
  const [date, setDate] = useState('')
  const [reason, setReason] = useState('')
  const [withdraw, setWithdraw] = useState(false)
  const [historyTarget, setHistoryTarget] = useState<Student | null>(null)
  const classes = useQuery({ queryKey: ['classes', 'admin', classSearch, classPage],
    queryFn: async () => (await api.get<Page<Room>>('/admin/classes', { params: { search: classSearch, page: classPage } })).data })
  const students = useQuery({ queryKey: ['students', 'admin', search, page, classId, status],
    queryFn: async () => (await api.get<Page<Student>>('/admin/students', { params: { search, page, classId: classId || undefined, status } })).data })
  const history = useQuery({ queryKey: ['enrollments', historyTarget?.id], enabled: !!historyTarget,
    queryFn: async () => (await api.get<Enrollment[]>(`/admin/students/${historyTarget!.id}/enrollments`)).data })
  const save = useMutation({ mutationFn: async () => {
    if (!editor) return
    if (editor.kind === 'class') return api.put(`/admin/classes/${editor.room.id}`, { name })
    if (editor.kind === 'student') return api.put(`/admin/students/${editor.student.id}`, { fullName: name, revision: editor.student.revision })
    return api.post(`/admin/students/${editor.student.id}/enrollments`, { classId: withdraw ? null : targetClass, effectiveDate: date, reason, revision: editor.student.revision })
  }, onSuccess: async () => { setEditor(null); toast.success('Đã lưu thay đổi.');
    await Promise.all(['classes', 'students', 'scope-options', 'enrollments', 'portions', 'parent-students'].map(key => cache.invalidateQueries({ queryKey: [key] }))) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  function open(next: Editor) {
    setEditor(next); setName(next.kind === 'class' ? next.room.name : next.student.fullName)
    setTargetClass(''); setReason(''); setWithdraw(false); setDate(students.data?.earliestChangeDate ?? '')
  }
  return <>
    <section className="panel workflow-lists"><div className="panel-head"><h2>Danh sách lớp</h2><input aria-label="Tìm lớp" placeholder="Tên lớp hoặc niên khóa" value={classSearch} onChange={e => { setClassSearch(e.target.value); setClassPage(1) }} /></div>
      {classes.isPending ? <p className="empty compact">Đang tải…</p> : classes.isError ? <p className="empty compact error">{apiErrorMessage(classes.error)}</p> : <>
        {!classes.data?.items.length && <p className="empty compact">Không có lớp phù hợp.</p>}
        {classes.data?.items.map(room => <div className="entry student-entry" key={room.id}><div><strong>{room.name}</strong><small>{room.schoolYear} · {room.studentCount} trẻ đang học hôm nay</small></div>
          <div className="entry-actions"><button type="button" className="button secondary" onClick={() => { setClassId(room.id); setPage(1) }}>Xem trẻ</button>
            <button type="button" className="button secondary" onClick={() => open({ kind: 'class', room })}>Sửa lớp</button></div></div>)}
      </>}
      <Pagination page={classPage} total={classes.data?.total ?? 0} pageSize={20} busy={classes.isFetching} onChange={setClassPage} />
    </section>
    <section className="panel workflow-lists"><h2>Danh sách trẻ</h2><div className="workflow-form"><ClassPicker value={classId} onChange={id => { setClassId(id); setPage(1) }} label="Lọc lớp hiện tại / lớp cuối nếu chưa học" />
      <div className="workflow-fields"><label className="field">Tìm trẻ<input placeholder="Mã trẻ hoặc họ tên" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Trạng thái hôm nay<select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="ACTIVE">Đang học</option><option value="INACTIVE">Chưa học / đã ngừng</option></select></label></div></div>
      {students.isPending ? <p className="empty compact">Đang tải…</p> : students.isError ? <p className="empty compact error">{apiErrorMessage(students.error)}</p> : <>
        {!students.data?.items.length && <p className="empty compact">Không có trẻ phù hợp.</p>}
        {students.data?.items.map(student => <div className="entry student-entry" key={student.id}><div><strong>{student.fullName}</strong><small>{student.studentCode} · {student.className} · {student.isActive ? 'Đang học' : 'Chưa học / đã ngừng'}</small>
          <small>{student.parents.length ? `Phụ huynh: ${student.parents.map(p => `${p.fullName} (${p.phoneNumber || p.email || ''})`).join(', ')}` : 'Chưa liên kết phụ huynh'}</small></div>
          <div className="entry-actions"><button type="button" className="button secondary" onClick={() => open({ kind: 'student', student })}>Sửa hồ sơ</button>
            <button type="button" className="button secondary" onClick={() => open({ kind: 'enrollment', student })}>Ghi danh / chuyển lớp</button>
            <button type="button" className="button secondary" onClick={() => setHistoryTarget(student)}>Lịch sử</button>
            <button type="button" className="button secondary" onClick={() => onLink(student)}>Liên kết phụ huynh</button></div></div>)}
      </>}
      <Pagination page={page} total={students.data?.total ?? 0} pageSize={20} busy={students.isFetching} onChange={setPage} />
    </section>
    {editor && <Modal title={editor.kind === 'class' ? 'Sửa lớp' : editor.kind === 'student' ? 'Sửa hồ sơ trẻ' : `Ghi danh: ${editor.student.fullName}`} busy={save.isPending} onClose={() => setEditor(null)}>
      <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!save.isPending) save.mutate() }}>
        {editor.kind !== 'enrollment' ? <><label className="field">{editor.kind === 'class' ? 'Tên lớp' : 'Họ tên trẻ'}<input required maxLength={editor.kind === 'class' ? 100 : 150} value={name} onChange={e => setName(e.target.value)} /></label>
          <p className="form-help">{editor.kind === 'class' ? `Niên khóa: ${editor.room.schoolYear}` : `Mã cố định: ${editor.student.studentCode}`}</p></> : <>
          <label className="field">Thao tác<select value={withdraw ? 'withdraw' : 'enroll'} onChange={e => setWithdraw(e.target.value === 'withdraw')}><option value="enroll">Chuyển lớp / ghi danh lại</option><option value="withdraw">Ngừng học</option></select></label>
          {!withdraw && <ClassPicker required value={targetClass} onChange={setTargetClass} label="Lớp tiếp nhận" />}
          <label className="field">Ngày hiệu lực<input required type="date" min={students.data?.earliestChangeDate} value={date} onChange={e => setDate(e.target.value)} /></label>
          <label className="field">Lý do<textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
          <p className="form-help">Sau 07:30, thay đổi áp dụng từ ngày mai. Ngày hiệu lực là ngày đầu học lớp mới hoặc ngày đầu ngừng học; phải sau ngày bắt đầu lần ghi danh gần nhất.</p>
        </>}
        <div className="form-actions"><button type="button" className="button secondary" disabled={save.isPending} onClick={() => setEditor(null)}>Hủy</button><button type="submit" className="button primary" disabled={save.isPending}>Lưu thay đổi</button></div>
      </form>
    </Modal>}
    {historyTarget && <Modal title={`Lịch sử: ${historyTarget.fullName}`} description={historyTarget.studentCode} onClose={() => setHistoryTarget(null)}>
      <div className="workflow-form">{history.isPending ? <p>Đang tải…</p> : history.isError ? <p className="error">{apiErrorMessage(history.error)}</p> : !history.data?.length ? <p>Chưa có lịch sử ghi danh.</p> : history.data.map(item =>
        <div className="entry" key={item.id}><strong>{item.className} · {item.schoolYear}</strong><small>{item.startDate} → {item.endDate ? `${item.endDate} (không bao gồm ngày này)` : 'Chưa kết thúc'}</small><p>{item.reason}</p>{item.endReason && <p>Kết thúc: {item.endReason}</p>}</div>)}</div>
    </Modal>}
  </>
}
