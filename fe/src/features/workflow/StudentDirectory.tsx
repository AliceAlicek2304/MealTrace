import { StudentProfileFields, displayBirth, displayGender } from './StudentProfileFields'
import { FilterPanel } from '../../components/FilterPanel'
import { RecordActions } from '../../components/RecordActions'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { ClassPicker } from '../../components/ClassPicker'
import { Pagination } from '../../components/Pagination'
import { QueryState } from '../../components/QueryState'
import { ArrowRightLeft, History, Pencil, UserRoundMinus, UserRoundPlus } from 'lucide-react'

type Page<T> = { items: T[]; total: number; earliestChangeDate?: string }
type Room = { id: string; name: string; schoolYear: string; studentCount: number }
type Student = { id: string; studentCode: string; fullName: string; dateOfBirth?: string | null; gender?: string | null; revision: number; isActive: boolean; className: string;
  parents: { id: string; fullName: string; email: string | null; phoneNumber: string | null }[] }
type Enrollment = { id: string; className: string; schoolYear: string; startDate: string; endDate: string | null; reason: string; endReason: string | null }
type Editor = { kind: 'class'; room: Room } | { kind: 'student' | 'enrollment'; student: Student }

function editorTitle(editor: Editor) {
  if (editor.kind === 'class') return 'Sửa lớp'
  if (editor.kind === 'student') return 'Sửa hồ sơ trẻ'
  return `Ghi danh: ${editor.student.fullName}`
}

function editorProfile(editor: Editor) {
  if (editor.kind === 'class') return { label: 'Tên lớp', maxLength: 100, help: `Niên khóa: ${editor.room.schoolYear}` }
  return { label: 'Họ tên trẻ', maxLength: 150, help: `Mã cố định: ${editor.student.studentCode}` }
}

export function StudentDirectory({ onLink, view, onCreate, onImport, onImportHistory, onViewStudents, initialClassId, isAdmin = true }: Readonly<{
  onLink: (student: { id: string; fullName: string }) => void; view: 'classes' | 'students'; onCreate: () => void; onImport?: () => void; onImportHistory?: () => void;
  onViewStudents: (classId: string) => void; initialClassId: string; isAdmin?: boolean
}>) {
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
  const [parentStatus, setParentStatus] = useState('')
  const [editor, setEditor] = useState<Editor | null>(null)
  const [name, setName] = useState('')
  const [birth, setBirth] = useState('')
  const [gender, setGender] = useState('')
  const [targetClass, setTargetClass] = useState('')
  const [date, setDate] = useState('')
  const [reason, setReason] = useState('')
  const [withdraw, setWithdraw] = useState(false)
  const [historyTarget, setHistoryTarget] = useState<Student | null>(null)
  const [profileTarget, setProfileTarget] = useState<Student | null>(null)
  const [parentsTarget, setParentsTarget] = useState<Student | null>(null)
  const classes = useQuery({ queryKey: ['classes', 'admin', classSearchTerm, classPage], enabled: !classSearchWaiting && view === 'classes',
    queryFn: async () => (await api.get<Page<Room>>('/admin/classes', { params: { search: classSearchTerm, page: classPage } })).data })
  const students = useQuery({ queryKey: ['students', isAdmin ? 'admin' : 'teacher', searchTerm, page, classId, status, parentStatus], enabled: !searchWaiting && view === 'students',
    queryFn: async () => (await api.get<Page<Student>>(isAdmin ? '/admin/students' : '/students/search', { params: { search: searchTerm, page, classId: classId || undefined, status, parentStatus } })).data })
  const history = useQuery({ queryKey: ['enrollments', historyTarget?.id], enabled: !!historyTarget,
    queryFn: async () => (await api.get<Enrollment[]>(`/admin/students/${historyTarget!.id}/enrollments`)).data })
  const save = useMutation({ mutationFn: async () => {
    if (!editor) throw new Error('Không có thay đổi cần lưu.')
    if (editor.kind === 'class') {
      await api.put(`/admin/classes/${editor.room.id}`, { name })
    } else if (editor.kind === 'student') {
      await api.put(`/admin/students/${editor.student.id}`, { fullName: name, updateProfile: true, dateOfBirth: birth || null, gender: gender || null, revision: editor.student.revision })
    } else {
      await api.post(`/admin/students/${editor.student.id}/enrollments`, { classId: withdraw ? null : targetClass, effectiveDate: date, reason, revision: editor.student.revision })
    }
  }, onSuccess: async () => { setEditor(null); toast.success('Đã lưu thay đổi.');
    await Promise.all(['classes', 'students', 'scope-options', 'enrollments', 'portions', 'parent-students'].map(key => cache.invalidateQueries({ queryKey: [key] }))) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  function open(next: Editor) {
    setEditor(next); setName(next.kind === 'class' ? next.room.name : next.student.fullName)
    setBirth(next.kind === 'class' ? '' : next.student.dateOfBirth ?? ''); setGender(next.kind === 'class' ? '' : next.student.gender ?? '')
    setTargetClass(''); setReason(''); setWithdraw(false); setDate(students.data?.earliestChangeDate ?? '')
  }
  const profile = editor && editorProfile(editor)
  return <>
    {view === 'classes' && <section className="panel workflow-lists"><div className="panel-head"><h2>Danh sách lớp</h2><button type="button" className="button primary" onClick={onCreate}>Tạo lớp</button></div>
      <FilterPanel activeCount={[classSearchTerm].filter(Boolean).length}><div className="list-toolbar"><label className="field">Tìm lớp<input placeholder="Tên lớp hoặc niên khóa" value={classSearch} onChange={e => { setClassSearch(e.target.value); setClassPage(1) }} /></label></div></FilterPanel>
      <SearchFeedback waiting={classSearchWaiting} fetching={classes.isFetching} />
      <QueryState loading={classes.isPending} error={classes.isError} loadingMessage="Đang tải…" errorMessage={apiErrorMessage(classes.error)}>
        {!classes.data?.items.length && <p className="empty compact">{classSearchTerm ? 'Không có lớp phù hợp tìm kiếm.' : 'Chưa có lớp.'}</p>}
        <div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Lớp</th><th scope="col">Năm học</th><th scope="col">Trẻ đang học</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {classes.data?.items.map(room => <tr key={room.id}><td><strong>{room.name}</strong></td><td>{room.schoolYear}</td><td>{room.studentCount}</td>
          <td><RecordActions label={room.name}>{close => <><button type="button" className="button secondary" onClick={() => { close(); setClassId(room.id); setPage(1); onViewStudents(room.id) }}>Xem trẻ</button>
            <button type="button" className="button secondary" onClick={() => { close(); open({ kind: 'class', room }) }}>Sửa lớp</button></>}</RecordActions></td></tr>)}
        </tbody></ResponsiveTable></div>
      </QueryState>
      <Pagination page={classPage} total={classes.data?.total ?? 0} pageSize={20} busy={classSearchWaiting || classes.isFetching} onChange={setClassPage} />
    </section>}
    {view === 'students' && <section className="panel workflow-lists"><div className="panel-head"><h2>{isAdmin ? 'Danh sách trẻ' : 'Trẻ trong lớp phụ trách'}</h2><StudentDirectoryActions isAdmin={isAdmin} onImportHistory={onImportHistory} onImport={onImport} onCreate={onCreate} /></div><FilterPanel activeCount={[searchTerm, classId, status, parentStatus].filter(Boolean).length}><div className="list-toolbar"><ClassPicker compact assignedOnly={!isAdmin} value={classId} onChange={id => { setClassId(id); setPage(1) }} label="Lọc lớp hiện tại / lớp cuối" />
      <label className="field">Tìm trẻ<input placeholder="Mã trẻ hoặc họ tên" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
        <label className="field">Trạng thái hôm nay<select value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="ACTIVE">Đang học</option><option value="INACTIVE">Chưa học / đã ngừng</option></select></label>
        <label className="field">Liên kết phụ huynh<select value={parentStatus} onChange={e => { setParentStatus(e.target.value); setPage(1) }}><option value="">Tất cả</option><option value="UNLINKED">Chưa liên kết</option><option value="LINKED">Đã liên kết</option></select></label></div></FilterPanel>
      <SearchFeedback waiting={searchWaiting} fetching={students.isFetching} />
      <QueryState loading={students.isPending} error={students.isError} loadingMessage="Đang tải…" errorMessage={apiErrorMessage(students.error)}>
        {!students.data?.items.length && <p className="empty compact">{searchTerm || classId || status || parentStatus ? 'Không có trẻ phù hợp bộ lọc.' : 'Chưa có trẻ.'}</p>}
        <div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Trẻ / mã trẻ</th><th scope="col">Lớp</th><th scope="col">Trạng thái</th><th scope="col">Phụ huynh</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {students.data?.items.map(student => <tr key={student.id}><td><button type="button" className="parent-summary" aria-label={`Xem hồ sơ ${student.fullName}`} onClick={() => setProfileTarget(student)}><strong>{student.fullName}</strong></button><small>{student.studentCode}</small></td><td>{student.className}</td><td><span className={`status ${student.isActive ? 'active' : 'suspended'}`}>{student.isActive ? 'Đang học' : 'Chưa học / đã ngừng'}</span></td>
          <td>{student.parents.length ? <button type="button" className="parent-summary" aria-label={`Xem ${student.parents.length} phụ huynh của ${student.fullName}`} onClick={() => setParentsTarget(student)}>
            {student.parents[0].fullName}{student.parents.length > 1 && <span aria-hidden="true"> …</span>}
          </button> : <span className="muted">Chưa liên kết</span>}</td>
          <td><RecordActions label={student.fullName}>{close => <>{isAdmin && <><button type="button" className="icon-button" title="Sửa hồ sơ" aria-label={`Sửa hồ sơ ${student.fullName}`} onClick={() => { close(); open({ kind: 'student', student }) }}><Pencil size={17} /><span>Sửa hồ sơ</span></button>
            <button type="button" className="icon-button" title="Ghi danh / chuyển lớp" aria-label={`Ghi danh / chuyển lớp ${student.fullName}`} onClick={() => { close(); open({ kind: 'enrollment', student }) }}><ArrowRightLeft size={17} /><span>Ghi danh / chuyển lớp</span></button>
            {student.isActive && <button type="button" className="icon-button action-danger" title="Ngừng học" aria-label={`Ngừng học ${student.fullName}`} onClick={() => { close(); open({ kind: 'enrollment', student }); setWithdraw(true) }}><UserRoundMinus size={17} /><span>Ngừng học</span></button>}
            <button type="button" className="icon-button" title="Lịch sử ghi danh" aria-label={`Lịch sử ${student.fullName}`} onClick={() => { close(); setHistoryTarget(student) }}><History size={17} /><span>Lịch sử ghi danh</span></button></>}
            <button type="button" className="icon-button" title="Liên kết phụ huynh" aria-label={`Liên kết phụ huynh ${student.fullName}`} onClick={() => { close(); onLink(student) }}><UserRoundPlus size={17} /><span>Liên kết phụ huynh</span></button></>}</RecordActions></td></tr>)}
        </tbody></ResponsiveTable></div>
      </QueryState>
      <Pagination page={page} total={students.data?.total ?? 0} pageSize={20} busy={searchWaiting || students.isFetching} onChange={setPage} />
    </section>}
    {editor && <Modal title={editorTitle(editor)} busy={save.isPending} onClose={() => setEditor(null)}>
      <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!save.isPending) save.mutate() }}>
        {editor.kind !== 'enrollment' ? <><label className="field">{profile!.label}<input required maxLength={profile!.maxLength} value={name} onChange={e => setName(e.target.value)} /></label>
          {editor.kind === 'student' && <StudentProfileFields birth={birth} gender={gender} onBirth={setBirth} onGender={setGender} />}
          <p className="form-help">{profile!.help}</p></> : <>
          <label className="field">Thao tác<select value={withdraw ? 'withdraw' : 'enroll'} onChange={e => setWithdraw(e.target.value === 'withdraw')}><option value="enroll">Chuyển lớp / ghi danh lại</option><option value="withdraw">Ngừng học</option></select></label>
          {!withdraw && <ClassPicker required value={targetClass} onChange={setTargetClass} label="Lớp tiếp nhận" />}
          <label className="field">Ngày hiệu lực<input required type="date" min={students.data?.earliestChangeDate} value={date} onChange={e => setDate(e.target.value)} /></label>
          <label className="field">Lý do<textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} /></label>
          <p className="form-help">Sau 07:30, thay đổi áp dụng từ ngày mai. Ngày hiệu lực là ngày đầu học lớp mới hoặc ngày đầu ngừng học; phải sau ngày bắt đầu lần ghi danh gần nhất.</p>
        </>}
        <div className="form-actions"><button type="button" className="button secondary" disabled={save.isPending} onClick={() => setEditor(null)}>Hủy</button><button type="submit" className="button primary" disabled={save.isPending}>Lưu thay đổi</button></div>
      </form>
    </Modal>}
    {profileTarget && <Modal title="Hồ sơ trẻ" description={profileTarget.studentCode} onClose={() => setProfileTarget(null)}><dl className="student-import-detail"><div><dt>Họ tên</dt><dd>{profileTarget.fullName}</dd></div><div><dt>Ngày sinh</dt><dd>{displayBirth(profileTarget.dateOfBirth)}</dd></div><div><dt>Giới tính</dt><dd>{displayGender(profileTarget.gender)}</dd></div><div><dt>Lớp</dt><dd>{profileTarget.className}</dd></div></dl></Modal>}
    {parentsTarget && <Modal title={`Phụ huynh: ${parentsTarget.fullName}`} description={parentsTarget.studentCode} onClose={() => setParentsTarget(null)}>
      <div className="parent-contact-list">{parentsTarget.parents.map(parent => <article className="parent-contact" key={parent.id}>
        <h3>{parent.fullName}</h3><dl><dt>SĐT</dt><dd>{parent.phoneNumber || 'Chưa có'}</dd><dt>Email</dt><dd>{parent.email || 'Chưa có'}</dd></dl>
      </article>)}</div>
    </Modal>}
    {historyTarget && <Modal title={`Lịch sử: ${historyTarget.fullName}`} description={historyTarget.studentCode} onClose={() => setHistoryTarget(null)}>
      <div className="workflow-form"><QueryState loading={history.isPending} error={history.isError} loadingMessage="Đang tải…" errorMessage={apiErrorMessage(history.error)} loadingClassName="form-help" errorClassName="error">{!history.data?.length ? <p>Chưa có lịch sử ghi danh.</p> : history.data.map(item =>
        <div className="entry" key={item.id}><strong>{item.className} · {item.schoolYear}</strong><small>{item.startDate} → {item.endDate ? `${item.endDate} (không bao gồm ngày này)` : 'Chưa kết thúc'}</small><p>{item.reason}</p>{item.endReason && <p>Kết thúc: {item.endReason}</p>}</div>)}</QueryState></div>
    </Modal>}
  </>
}

function StudentDirectoryActions({ isAdmin, onImportHistory, onImport, onCreate }: Readonly<{ isAdmin: boolean; onImportHistory?: () => void; onImport?: () => void; onCreate: () => void }>) {
  return <div className="student-directory-actions">{isAdmin && onImportHistory && <button type="button" className="button secondary" onClick={onImportHistory}>Lịch sử nhập</button>}{isAdmin && onImport && <button type="button" className="button secondary" onClick={onImport}>Nhập trẻ từ Excel</button>}<button type="button" className="button primary" onClick={onCreate}>Thêm trẻ</button></div>
}
