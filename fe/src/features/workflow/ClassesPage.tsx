import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { ClassPicker } from '../../components/ClassPicker'
import { StudentDirectory } from './StudentDirectory'
import { AcademicYears } from './AcademicYears'
import { SchoolYearPicker } from '../../components/SchoolYearPicker'
import { toast } from 'sonner'

type Student = { id: string; fullName: string; classId: string }
type NotificationResult = { status: string; message: string; providerMessageId: string | null }
type LinkResult = { parentId: string; fullName: string; email: string | null; phoneNumber: string | null; created: boolean; temporaryPassword: string | null; notification: NotificationResult | null }

function notificationMessage(result: NotificationResult) {
  if (result.status === 'ACCEPTED') return 'Yêu cầu gửi tin đã được tiếp nhận. Hãy kiểm tra nội dung trên WhatsApp của phụ huynh.'
  if (result.status === 'UNKNOWN') return 'Hồ sơ đã lưu; chưa xác định được kết quả gửi tin. Hãy nhờ quản trị viên kiểm tra trước khi gửi lại.'
  if (result.status === 'RATE_LIMITED') return 'Hồ sơ đã lưu; vui lòng đợi ít nhất 60 giây trước lần gửi tiếp theo.'
  return 'Hồ sơ đã lưu nhưng chưa gửi được tin. Hãy liên hệ quản trị viên để được hỗ trợ.'
}

export function ClassesPage({ isAdmin = true }: { isAdmin?: boolean }) {
  const queryClient = useQueryClient()
  const notifications = useQuery({ queryKey: ['notification-settings'], queryFn: async () =>
    (await api.get<{ enabled: boolean; channel: string; templateOnly: boolean }>('/notifications/settings')).data })
  const channel = notifications.data?.channel ?? 'Thông báo'

  const [tab, setTab] = useState<'students' | 'classes' | 'years'>('students')
  const [creating, setCreating] = useState<'class' | 'student' | null>(null)
  const [name, setName] = useState('')
  const [schoolYear, setSchoolYear] = useState('')
  const [studentName, setStudentName] = useState('')
  const [studentCode, setStudentCode] = useState('')
  const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  const [startDate, setStartDate] = useState(today)
  const [selectedStudentName, setSelectedStudentName] = useState('')
  const [classId, setClassId] = useState('')
  const [selectedStudentId, setSelectedStudentId] = useState('')
  const [parentPhone, setParentPhone] = useState('')
  const [parentName, setParentName] = useState('')
  const [credentialPhone, setCredentialPhone] = useState('')
  const [temporaryPassword, setTemporaryPassword] = useState('')
  const [sendRegistrationNotification, setSendRegistrationNotification] = useState(false)
  const [notificationResult, setNotificationResult] = useState<NotificationResult | null>(null)
  const addClass = useMutation({ mutationFn: () => api.post('/classes', { name, schoolYear }),
    onSuccess: async () => { setCreating(null); setName(''); toast.success('Đã tạo lớp.'); await Promise.all([queryClient.invalidateQueries({ queryKey: ['classes'] }), queryClient.invalidateQueries({ queryKey: ['scope-options'] }), queryClient.invalidateQueries({ queryKey: ['admin-academic-years'] })]) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  const addStudent = useMutation({ mutationFn: () => api.post<Student>('/students', { fullName: studentName, classId, studentCode: studentCode || null, startDate }),
    onSuccess: async () => { setCreating(null); setStudentName(''); setStudentCode(''); toast.success('Đã thêm trẻ và ghi danh vào lớp. Có thể liên kết phụ huynh sau khi có SĐT.'); await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['students'] }), queryClient.invalidateQueries({ queryKey: ['classes'] }),
      queryClient.invalidateQueries({ queryKey: ['scope-options'] })]) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  const linkParent = useMutation({ mutationFn: async () => (await api.post<LinkResult>(`/admin/students/${selectedStudentId}/parents`,
    { phoneNumber: parentPhone, fullName: parentName, sendRegistrationNotification })).data,
    onSuccess: async result => {
      setTemporaryPassword(result.temporaryPassword ?? ''); setCredentialPhone(result.phoneNumber ?? result.email ?? '')
      setNotificationResult(result.notification)
      setParentPhone(''); setParentName('')
      toast.success(result.created ? 'Đã tạo tài khoản phụ huynh và liên kết trẻ.' : 'Đã liên kết trẻ với tài khoản phụ huynh hiện có.', { toasterId: 'edit-modal' })
      await Promise.all([queryClient.invalidateQueries({ queryKey: ['students'] }),
        queryClient.invalidateQueries({ queryKey: ['admin-users'] }), queryClient.invalidateQueries({ queryKey: ['scope-options'] })])
    },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })

  function submitClass(event: FormEvent) { event.preventDefault(); addClass.mutate() }
  function submitStudent(event: FormEvent) { event.preventDefault(); addStudent.mutate() }
  function submitLink(event: FormEvent) { event.preventDefault(); if (linkParent.isPending) return; setTemporaryPassword(''); setNotificationResult(null); linkParent.mutate() }
  function openParent(student: Student | { id: string; fullName: string }) {
    setSelectedStudentId(student.id); setSelectedStudentName(student.fullName); setParentPhone(''); setParentName(''); setTemporaryPassword(''); setNotificationResult(null); setSendRegistrationNotification(false)
  }

  return <>
    <div className="eyebrow">DỮ LIỆU NỀN</div><h1>Lớp và trẻ</h1>
    <p className="lead">Thêm trẻ vào lớp ngay cả khi chưa có SĐT phụ huynh. Liên kết phụ huynh sau khi có thông tin; một trẻ có thể có nhiều người giám hộ.</p>
    {isAdmin && <div className="list-tabs" aria-label="Danh mục quản lý">
      <button type="button" aria-pressed={tab === 'students'} onClick={() => setTab('students')}>Trẻ</button>
      <button type="button" aria-pressed={tab === 'classes'} onClick={() => setTab('classes')}>Lớp</button>
      <button type="button" aria-pressed={tab === 'years'} onClick={() => setTab('years')}>Năm học</button>
    </div>}
    {isAdmin && tab === 'years' ? <AcademicYears /> : <StudentDirectory isAdmin={isAdmin} view={isAdmin ? tab as 'classes' | 'students' : 'students'} onCreate={() => { setCreating(isAdmin && tab === 'classes' ? 'class' : 'student'); setName(''); setStudentName(''); setStudentCode(''); setStartDate(today) }} onViewStudents={id => { setClassId(id); setTab('students') }} initialClassId={classId} onLink={openParent} />}
    {creating === 'class' && <Modal title="Tạo lớp" busy={addClass.isPending} onClose={() => setCreating(null)}><form className="workflow-form" onSubmit={submitClass}>
        <label className="field">Tên lớp<input required maxLength={100} value={name} onChange={e => setName(e.target.value)} /></label>
        <SchoolYearPicker value={schoolYear} onChange={setSchoolYear} />
        <button type="submit" className="button primary" disabled={addClass.isPending}>Lưu lớp</button>
      </form></Modal>}
    {creating === 'student' && <Modal title="Thêm trẻ" busy={addStudent.isPending} onClose={() => setCreating(null)}><form className="workflow-form" onSubmit={submitStudent}>
        <p className="form-help">Chưa cần SĐT hoặc tài khoản phụ huynh. Trẻ vẫn được ghi danh vào lớp; bạn có thể dùng “Liên kết phụ huynh” khi đã có thông tin.</p>
        <ClassPicker assignedOnly={!isAdmin} required value={classId} onChange={setClassId} />
        <label className="field">Họ tên trẻ<input required maxLength={150} value={studentName} onChange={e => setStudentName(e.target.value)} /></label>
        <label className="field">Mã trẻ (bỏ trống để tự tạo)<input maxLength={40} value={studentCode} onChange={e => setStudentCode(e.target.value)} placeholder="HS-2026-0012" /></label>
        <label className="field">Ngày bắt đầu học<input type="date" required min={today} value={startDate} onChange={e => setStartDate(e.target.value)} /></label>
        <button type="submit" className="button primary" disabled={addStudent.isPending}>Lưu trẻ</button>
      </form></Modal>}
    {selectedStudentId && <Modal title={`Liên kết phụ huynh cho ${selectedStudentName || 'trẻ vừa tạo'}`} busy={linkParent.isPending} onClose={() => { setSelectedStudentId(''); setParentPhone(''); setParentName(''); setTemporaryPassword('') }}>
      <form className="workflow-form" onSubmit={submitLink}><p className="form-help">Nhập SĐT đã có tài khoản để liên kết ngay. Nếu SĐT chưa tồn tại, nhập thêm họ tên để tạo tài khoản phụ huynh mới; mật khẩu tạm chỉ hiển thị một lần.</p>
        <div className="workflow-fields"><label className="field">SĐT phụ huynh<input required type="tel" maxLength={30} value={parentPhone} onChange={e => setParentPhone(e.target.value)} /></label>
          <label className="field">Họ tên nếu tạo mới<input maxLength={120} value={parentName} onChange={e => setParentName(e.target.value)} /></label>
          <label className="switch-line"><input type="checkbox" disabled={!notifications.data?.enabled} checked={sendRegistrationNotification} onChange={e => setSendRegistrationNotification(e.target.checked)} /> Gửi hướng dẫn đăng nhập qua {channel}</label>
          <button type="submit" className="button primary" disabled={linkParent.isPending}>Liên kết</button></div></form>
      <p className="form-help">Hồ sơ và tài khoản vẫn được lưu nếu gửi tin không thành công. Phụ huynh cần dùng WhatsApp để nhận tin.</p>
      {isAdmin && <details className="notification-details"><summary>Thông tin cấu hình gửi thử</summary><p>{notifications.data?.templateOnly ? 'Twilio dùng mẫu demo cố định, chưa chứa thông tin đăng nhập.' : 'Ưu tiên Vonage gửi tên trẻ và thông tin đăng nhập; Twilio dự phòng dùng mẫu demo cố định.'} Chỉ gửi tới số thử đã cấu hình; cần kết nối môi trường thử và mở hội thoại trước. Đợi 60 giây giữa các lần gửi. Gửi tin không xác minh quyền sở hữu SĐT.</p></details>}
      {notificationResult && <p className={notificationResult.status === 'ACCEPTED' ? 'form-help' : 'error'} role="status">{isAdmin ? `${channel} ${notificationResult.status}: ${notificationResult.message}` : notificationMessage(notificationResult)}</p>}
      {temporaryPassword && <div className="credential-once"><strong>Đăng nhập: {credentialPhone} · Mật khẩu tạm:</strong> <code>{temporaryPassword}</code><button type="button" onClick={() => setTemporaryPassword('')}>Đã lưu, ẩn mật khẩu</button><small>Chỉ hiển thị một lần. Chuyển riêng cho phụ huynh qua kênh an toàn.</small></div>}
    </Modal>}
  </>
}
