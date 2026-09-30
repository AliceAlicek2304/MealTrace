import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { toast } from 'sonner'

type SchoolClass = { id: string; name: string; schoolYear: string; studentCount: number }
type Student = { id: string; fullName: string; classId: string; parents: { id: string; fullName: string; email: string; phoneNumber: string | null }[] }
type LinkResult = { parentId: string; fullName: string; email: string; phoneNumber: string | null; created: boolean; temporaryPassword: string | null }

export function ClassesPage() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [schoolYear, setSchoolYear] = useState('2026-2027')
  const [studentName, setStudentName] = useState('')
  const [classId, setClassId] = useState('')
  const [selectedStudentId, setSelectedStudentId] = useState('')
  const [parentPhone, setParentPhone] = useState('')
  const [parentName, setParentName] = useState('')
  const [credentialPhone, setCredentialPhone] = useState('')
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const classes = useQuery({ queryKey: ['classes'], queryFn: async () => (await api.get<SchoolClass[]>('/classes')).data })
  const students = useQuery({ queryKey: ['students', classId], enabled: !!classId,
    queryFn: async () => (await api.get<Student[]>(`/classes/${classId}/students`)).data })
  const addClass = useMutation({ mutationFn: () => api.post('/classes', { name, schoolYear }),
    onSuccess: async () => { setName(''); toast.success('Đã tạo lớp.'); await queryClient.invalidateQueries({ queryKey: ['classes'] }) },
    onError: error => toast.error(apiErrorMessage(error)) })
  const addStudent = useMutation({ mutationFn: () => api.post<Student>('/students', { fullName: studentName, classId }),
    onSuccess: async response => { setStudentName(''); setSelectedStudentId(response.data.id); toast.success('Đã thêm trẻ. Bạn có thể liên kết phụ huynh trong cửa sổ đang mở.'); await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['students', classId] }), queryClient.invalidateQueries({ queryKey: ['classes'] }),
      queryClient.invalidateQueries({ queryKey: ['scope-options'] })]) },
    onError: error => toast.error(apiErrorMessage(error)) })
  const linkParent = useMutation({ mutationFn: async () => (await api.post<LinkResult>(`/admin/students/${selectedStudentId}/parents`,
    { phoneNumber: parentPhone, fullName: parentName })).data,
    onSuccess: async result => {
      setTemporaryPassword(result.temporaryPassword ?? ''); setCredentialPhone(result.phoneNumber ?? result.email)
      setParentPhone(''); setParentName('')
      toast.success(result.created ? 'Đã tạo tài khoản phụ huynh và liên kết trẻ.' : 'Đã liên kết trẻ với tài khoản phụ huynh hiện có.', { toasterId: 'edit-modal' })
      await Promise.all([queryClient.invalidateQueries({ queryKey: ['students', classId] }),
        queryClient.invalidateQueries({ queryKey: ['admin-users'] }), queryClient.invalidateQueries({ queryKey: ['scope-options'] })])
    },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })

  function submitClass(event: FormEvent) { event.preventDefault(); addClass.mutate() }
  function submitStudent(event: FormEvent) { event.preventDefault(); addStudent.mutate() }
  function submitLink(event: FormEvent) { event.preventDefault(); setTemporaryPassword(''); linkParent.mutate() }
  const selectedStudent = students.data?.find(student => student.id === selectedStudentId)

  return <>
    <div className="eyebrow">DỮ LIỆU NỀN</div><h1>Lớp và trẻ</h1>
    <p className="lead">Tạo lớp, thêm trẻ và liên kết phụ huynh bằng SĐT. Một trẻ có thể có nhiều người giám hộ.</p>
    <div className="grid">
      <section className="panel"><h2>Tạo lớp</h2><form className="workflow-form" onSubmit={submitClass}>
        <label className="field">Tên lớp<input required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></label>
        <label className="field">Niên khóa<input required pattern="[0-9]{4}-[0-9]{4}" value={schoolYear} onChange={e => setSchoolYear(e.target.value)} /></label>
        <button type="submit" className="button primary" disabled={addClass.isPending}>Lưu lớp</button>
      </form></section>
      <section className="panel"><h2>Thêm trẻ</h2><form className="workflow-form" onSubmit={submitStudent}>
        <label className="field">Lớp<select required value={classId} onChange={e => { setClassId(e.target.value); setSelectedStudentId(''); setTemporaryPassword('') }}><option value="">Chọn lớp</option>
          {classes.data?.map(room => <option key={room.id} value={room.id}>{room.name} · {room.schoolYear}</option>)}</select></label>
        <label className="field">Họ tên trẻ<input required maxLength={150} value={studentName} onChange={e => setStudentName(e.target.value)} /></label>
        <button type="submit" className="button primary" disabled={addStudent.isPending}>Lưu trẻ</button>
      </form></section>
    </div>
    <div className="grid workflow-lists">
      <section className="panel"><h2>Danh sách lớp</h2>{classes.isPending ? <p className="empty compact">Đang tải…</p> : classes.isError ? <p className="empty compact error">Không tải được lớp.</p> : !classes.data?.length ? <p className="empty compact">Chưa có lớp.</p> : classes.data.map(room =>
        <div className="entry" key={room.id}>{room.name}<small>{room.schoolYear} · {room.studentCount} trẻ đang hoạt động</small></div>)}</section>
      <section className="panel"><h2>Trẻ trong lớp đã chọn</h2>{!classId ? <p className="empty compact">Chọn một lớp ở trên.</p> : students.isPending ? <p className="empty compact">Đang tải…</p> : students.isError ? <p className="empty compact error">Không tải được trẻ.</p> : !students.data?.length ? <p className="empty compact">Chưa có trẻ.</p> : students.data.map(student =>
        <div className="entry student-entry" key={student.id}><div><strong>{student.fullName}</strong><small>{student.parents.length ? `Phụ huynh: ${student.parents.map(parent => `${parent.fullName} (${parent.phoneNumber || parent.email})`).join(', ')}` : 'Chưa liên kết phụ huynh'}</small></div><button type="button" className="button secondary" onClick={() => { setSelectedStudentId(student.id); setParentPhone(''); setParentName(''); setTemporaryPassword('') }}>Liên kết phụ huynh</button></div>)}</section>
    </div>
    {selectedStudentId && <Modal title={`Liên kết phụ huynh cho ${selectedStudent?.fullName ?? 'trẻ vừa tạo'}`} busy={linkParent.isPending} onClose={() => { setSelectedStudentId(''); setParentPhone(''); setParentName(''); setTemporaryPassword('') }}>
      <form className="workflow-form" onSubmit={submitLink}><p className="form-help">Nhập SĐT đã có tài khoản để liên kết ngay. Nếu SĐT chưa tồn tại, nhập thêm họ tên để tạo tài khoản phụ huynh mới; mật khẩu tạm chỉ hiển thị một lần.</p>
        <div className="workflow-fields"><label className="field">SĐT phụ huynh<input required type="tel" maxLength={30} value={parentPhone} onChange={e => setParentPhone(e.target.value)} /></label>
          <label className="field">Họ tên nếu tạo mới<input maxLength={120} value={parentName} onChange={e => setParentName(e.target.value)} /></label>
          <button type="submit" className="button primary" disabled={linkParent.isPending}>Liên kết</button></div></form>
      {temporaryPassword && <div className="credential-once"><strong>Đăng nhập: {credentialPhone} · Mật khẩu tạm:</strong> <code>{temporaryPassword}</code><button type="button" onClick={() => setTemporaryPassword('')}>Đã lưu, ẩn mật khẩu</button><small>Chỉ hiển thị một lần. Chuyển riêng cho phụ huynh qua kênh an toàn.</small></div>}
    </Modal>}
  </>
}
