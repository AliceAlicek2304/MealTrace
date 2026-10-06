import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { ClassPicker } from '../../components/ClassPicker'
import { Pagination } from '../../components/Pagination'
import { ArrowRightLeft, History, Pencil, UserRoundMinus, UserRoundPlus } from 'lucide-react'

type Page<T> = { items: T[]; total: number; earliestChangeDate?: string }
type Room = { id: string; name: string; schoolYear: string; studentCount: number }
type Student = { id: string; studentCode: string; fullName: string; revision: number; isActive: boolean; className: string;
  parents: { id: string; fullName: string; email: string | null; phoneNumber: string | null }[] }
type Enrollment = { id: string; className: string; schoolYear: string; startDate: string; endDate: string | null; reason: string; endReason: string | null }
type Editor = { kind: 'class'; room: Room } | { kind: 'student' | 'enrollment'; student: Student }

export function StudentDirectory({ onLink, view, onCreate, onViewStudents, initialClassId }: {
  onLink: (student: { id: string; fullName: string }) => void; view: 'classes' | 'students'; onCreate: () => void;
  onViewStudents: (classId: string) => void; initialClassId: string
}) {
  const cache = useQueryClient()
  const [classSearch, setClassSearch] = useState('')
  const classSearchTerm = useDebouncedValue(classSearch.trim())
  const classSearchWaiting = classSearch.trim() !== classSearchTerm
  const [classPage, setClassPage] = useState(1)
  const [search, setSearch] = useState('')
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [page, setPage] = useState(1)
  const [classId, setClassId] = useState(initialClassId)
  const [status, setStatus] = useState('')
  const [editor, setEditor] = useState<Editor | null>(null)
  const [name, setName] = useState('')
  const [targetClass, setTargetClass] = useState('')
  const [date, setDate] = useState('')
  const [reason, setReason] = useState('')
  const [withdraw, setWithdraw] = useState(false)
  const [historyTarget, setHistoryTarget] = useState<Student | null>(null)
  const classes = useQuery({ queryKey: ['classes', 'admin', classSearchTerm, classPage], enabled: !classSearchWaiting && view === 'classes',
    queryFn: async () => (await api.get<Page<Room>>('/admin/classes', { params: { search: classSearchTerm, page: classPage } })).data })
  const students = useQuery({ queryKey: ['students', 'admin', searchTerm, page, classId, status], enabled: !searchWaiting && view === 'students',
    queryFn: async () => (await api.get<Page<Student>>('/admin/students', { params: { search: searchTerm, page, classId: classId || undefined, status } })).data })
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
    {view === 'classes' && <section className="panel workflow-lists"><div className="panel-head"><h2>Danh sách lớp</h2><button type="button" className="button primary" onClick={onCreate}>Tạo lớp</button></div>
      <div className="list-toolbar"><label className="field">Tìm lớp<input placeholder="Tên lớp hoặc niên khóa" value={classSearch} onChange={e => { setClassSearch(e.target.value); setClassPage(1) }} /></label></div>
      <SearchFeedback waiting={classSearchWaiting} fetching={classes.isFetching} />
      {classes.isPending ? <p className="empty compact">Đang tải…</p> : classes.isError ? <p className="empty compact error">{apiErrorMessage(classes.error)}</p> : <>
        {!classes.data?.items.length && <p className="empty compact">{classSearchTerm ? 'Không có lớp phù hợp tìm kiếm.' : 'Chưa có lớp.'}</p>}
        <div className="table-wrap"><table><thead><tr><th scope="col">Lớp</th><th scope="col">Năm học</th><th scope="col">Trẻ đang học</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {classes.data?.items.map(room => <tr key={room.id}><td><strong>{room.name}</strong></td><td>{room.schoolYear}</td><td>{room.studentCount}</td>
          <td><div className="table-actions"><button type="button" className="button secondary" onClick={() => { setClassId(room.id); setPage(1); onViewStudents(room.id) }}>Xem trẻ</button>
            <button type="button" className="button secondary" onClick={() => open({ kind: 'class', room })}>Sửa lớp</button></div></td></tr>)}
        </tbody></table></div>
      </>}
      <Pagination page={classPage} total={classes.data?.total ?? 0} pageSize={20} busy={classSearchWaiting || classes.isFetching} onChange={setClassPage} />
    </section>}
    {view === 'students' && <section className="panel workflow-lists"><div className="panel-head"><h2>Danh sách trẻ</h2><button type="button" className="button primary" onClick={onCreate}>Thêm trẻ</button></div><div className="list-toolbar"><ClassPicker compact value={classId} onChange={id => { setClassId(id); setPage(1) }} label="Lọc lớp hiện tại / lớp cuối" />
      <label className="field">Tìm trẻ<input placeholder="Mã trẻ hoặc họ tên" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Trạng thái hôm nay<select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="ACTIVE">Đang học</option><option value="INACTIVE">Chưa học / đã ngừng</option></select></label></div>
      <SearchFeedback waiting={searchWaiting} fetching={students.isFetching} />
      {students.isPending ? <p className="empty compact">Đang tải…</p> : students.isError ? <p className="empty compact error">{apiErrorMessage(students.error)}</p> : <>
        {!students.data?.items.length && <p className="empty compact">{searchTerm || classId || status ? 'Không có trẻ phù hợp bộ lọc.' : 'Chưa có trẻ.'}</p>}
        <div className="table-wrap"><table><thead><tr><th scope="col">Trẻ / mã trẻ</th><th scope="col">Lớp</th><th scope="col">Trạng thái</th><th scope="col">Phụ huynh</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {students.data?.items.map(student => <tr key={student.id}><td><strong>{student.fullName}</strong><small>{student.studentCode}</small></td><td>{student.className}</td><td><span className={`status ${student.isActive ? 'active' : 'suspended'}`}>{student.isActive ? 'Đang học' : 'Chưa học / đã ngừng'}</span></td>
          <td>{student.parents.length ? student.parents.map(p => <div key={p.id}>{p.fullName}<small>{p.phoneNumber || p.email || 'Chưa có liên hệ'}</small></div>) : <span className="muted">Chưa liên kết</span>}</td>
          <td><div className="table-actions"><button type="button" className="icon-button" title="Sửa hồ sơ" aria-label={`Sửa hồ sơ ${student.fullName}`} onClick={() => open({ kind: 'student', student })}><Pencil size={17} /></button>
            <button type="button" className="icon-button" title="Ghi danh / chuyển lớp" aria-label={`Ghi danh / chuyển lớp ${student.fullName}`} onClick={() => open({ kind: 'enrollment', student })}><ArrowRightLeft size={17} /></button>
            {student.isActive && <button type="button" className="icon-button action-danger" title="Ngừng học" aria-label={`Ngừng học ${student.fullName}`} onClick={() => { open({ kind: 'enrollment', student }); setWithdraw(true) }}><UserRoundMinus size={17} /></button>}
            <button type="button" className="icon-button" title="Lịch sử ghi danh" aria-label={`Lịch sử ${student.fullName}`} onClick={() => setHistoryTarget(student)}><History size={17} /></button>
            <button type="button" className="icon-button" title="Liên kết phụ huynh" aria-label={`Liên kết phụ huynh ${student.fullName}`} onClick={() => onLink(student)}><UserRoundPlus size={17} /></button></div></td></tr>)}
        </tbody></table></div>
      </>}
      <Pagination page={page} total={students.data?.total ?? 0} pageSize={20} busy={searchWaiting || students.isFetching} onChange={setPage} />
    </section>}
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
