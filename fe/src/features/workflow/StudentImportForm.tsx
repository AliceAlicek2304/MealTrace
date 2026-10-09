import { useState, type FormEvent } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ClassPicker } from '../../components/ClassPicker'
import { api, apiErrorMessage } from '../../lib/api'
import { toast } from 'sonner'
import { Modal } from '../../components/Modal'

type Preview = { sheetName: string; ignoredColumns: string[]; canImport: boolean; rows: { row: number; fullName: string; dateOfBirth?: string | null; gender?: string | null; parentPhoneNumber?: string | null; error: string | null }[] }
type ImportRow = Preview['rows'][number]

function displayBirth(value?: string | null) { return value ? value.split('-').reverse().join('/') : 'Chưa có' }
function displayGender(value?: string | null) { return ({ MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' } as Record<string, string>)[value ?? ''] ?? 'Chưa có' }

export function StudentImportForm({ initialClassId = '', onDone, onBusy }: { initialClassId?: string; onDone: () => void; onBusy: (busy: boolean) => void }) {
  const client = useQueryClient()
  const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  const [classId, setClassId] = useState(initialClassId)
  const [startDate, setStartDate] = useState(today)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<Preview | null>(null)
  const [confirmed, setConfirmed] = useState(false)
  const [detail, setDetail] = useState<ImportRow | null>(null)
  const upload = useMutation({
    mutationFn: async (commit: boolean) => {
      const form = new FormData()
      form.append('file', file!); form.append('classId', classId); form.append('startDate', startDate)
      return (await api.post<Preview | { created: number }>(`/admin/students/import/${commit ? 'confirm' : 'preview'}`, form)).data
    },
    onMutate: () => onBusy(true),
    onSuccess: async result => {
      if ('created' in result) {
        await Promise.all(['students', 'classes', 'scope-options'].map(key => client.invalidateQueries({ queryKey: [key] })))
        toast.success(`Đã nhập ${result.created} trẻ và ghi danh vào lớp.`)
        onDone()
      } else {
        setPreview(result); setConfirmed(false)
        if (!result.canImport) toast.error('Danh sách có dòng lỗi hoặc trùng. Kiểm tra các dòng được đánh dấu trước khi nhập.', { toasterId: 'edit-modal' })
      }
    },
    onError: failure => { setPreview(null); setConfirmed(false); toast.error(apiErrorMessage(failure), { toasterId: 'edit-modal' }) },
    onSettled: () => onBusy(false),
  })
  function reset() { setPreview(null); setConfirmed(false); setDetail(null) }
  function submit(event: FormEvent) {
    event.preventDefault()
    if (!file || !classId || !startDate || upload.isPending) return
    if (!file.name.toLowerCase().endsWith('.xlsx') || file.size > 5 * 1024 * 1024 || file.size === 0) { toast.error('Chọn file XLSX không rỗng, tối đa 5 MB.', { toasterId: 'edit-modal' }); return }
    upload.mutate(false)
  }
  return <form className="workflow-form" onSubmit={submit}>
    <p className="form-help">Nhập họ tên, ngày sinh và giới tính có trong file; tự tạo mã trẻ. Lớp, ngày bắt đầu do Admin chọn bên dưới. Không tạo phụ huynh hoặc gửi WhatsApp.</p>
    <ClassPicker compact required assignedOnly showSchoolYear disabled={upload.isPending} value={classId} onChange={value => { setClassId(value); reset() }} />
    <label className="field">Ngày bắt đầu học<input type="date" required min={today} disabled={upload.isPending} value={startDate} onChange={event => { setStartDate(event.target.value); reset() }} /></label>
    <label className="field">File danh sách XLSX<input type="file" required accept=".xlsx" disabled={upload.isPending} onChange={event => { setFile(event.target.files?.[0] ?? null); reset() }} /></label>
    <p className="form-help">Một sheet, tối đa 500 trẻ, 5 MB. Cần cột “Họ tên” hoặc “Họ tên trẻ”; có thể có tiêu đề phía trên. Dòng lỗi/trùng phải sửa trước khi nhập.</p>
    <button className="button secondary" type="submit" disabled={upload.isPending || !file || !classId}>Xem trước danh sách</button>
    {preview && <Modal wide title="Kiểm tra danh sách trẻ" description={`${preview.rows.length} trẻ · ${preview.sheetName} · Ngày bắt đầu: ${displayBirth(startDate)}`} busy={upload.isPending} onClose={reset}><section aria-label="Danh sách nhập thử" className="student-import-preview">
      <details className="student-import-help"><summary>Hướng dẫn kiểm tra</summary><p className="form-help">Kiểm tra thông tin trước khi nhập. Thông tin thiếu hiển thị “Chưa có”. SĐT chỉ hiển thị để đối chiếu, chưa lưu hoặc tạo phụ huynh.</p>
      {preview.ignoredColumns.length > 0 && <p className="form-help">Các cột không nhập: {preview.ignoredColumns.join(', ')}.</p>}
      </details>
      {!preview.canImport && <p role="alert" className="error">Có dòng lỗi hoặc trùng. Sửa file rồi xem trước lại; chưa lưu trẻ nào.</p>}
      <div className="student-import-table"><table><thead><tr><th scope="col">Dòng</th><th scope="col">Họ tên trẻ</th><th scope="col">Ngày sinh</th><th scope="col">Giới tính</th><th scope="col">SĐT phụ huynh</th><th scope="col">Kiểm tra</th></tr></thead><tbody>{preview.rows.map(row => <tr key={row.row} className={row.error ? 'import-row-error' : ''}><td>{row.row}</td><td><strong>{row.fullName || '(Thiếu họ tên)'}</strong></td><td>{displayBirth(row.dateOfBirth)}</td><td>{displayGender(row.gender)}</td><td>{row.parentPhoneNumber || 'Chưa có'}</td><td>{row.error ? <span className="error">{row.error}</span> : <span className="status active">Hợp lệ</span>}</td></tr>)}</tbody></table></div>
      <ul className="student-import-cards" aria-label="Danh sách trẻ rút gọn">{preview.rows.map(row => <li key={row.row}><button type="button" className="student-import-card" aria-label={`Xem chi tiết ${row.fullName || `dòng ${row.row}`}`} onClick={() => setDetail(row)}><strong>{row.fullName || '(Thiếu họ tên)'}</strong><span className={`status ${row.error ? 'suspended' : 'active'}`}>{row.error ? 'Có lỗi' : 'Hợp lệ'}</span><span className="student-import-phone">SĐT: {row.parentPhoneNumber || 'Chưa có'}</span><span className="student-import-detail-link">Xem chi tiết ›</span></button></li>)}</ul>
      <div className="student-import-footer">
      <label className="switch-line"><input type="checkbox" disabled={!preview.canImport || upload.isPending} checked={confirmed} onChange={event => setConfirmed(event.target.checked)} /> Tôi đã kiểm tra danh sách và lớp nhận trẻ</label>
      <div className="form-actions"><button className="button secondary" type="button" disabled={upload.isPending} onClick={reset}>Quay lại chọn file</button><button className="button primary" type="button" disabled={!preview.canImport || !confirmed || upload.isPending} onClick={() => upload.mutate(true)}>{upload.isPending ? 'Đang xử lý…' : `Nhập ${preview.rows.length} trẻ`}</button></div>
      </div>
    </section></Modal>}
    {detail && <Modal title="Chi tiết trẻ nhập từ Excel" onClose={() => setDetail(null)}><dl className="student-import-detail"><div><dt>Họ tên trẻ</dt><dd>{detail.fullName || 'Chưa có'}</dd></div><div><dt>Dòng trong file</dt><dd>{detail.row}</dd></div><div><dt>Ngày sinh</dt><dd>{displayBirth(detail.dateOfBirth)}</dd></div><div><dt>Giới tính</dt><dd>{displayGender(detail.gender)}</dd></div><div><dt>SĐT phụ huynh</dt><dd>{detail.parentPhoneNumber || 'Chưa có'}</dd></div><div><dt>Kiểm tra</dt><dd>{detail.error || 'Hợp lệ'}</dd></div></dl><div className="student-import-detail-actions"><button type="button" className="button secondary" onClick={() => setDetail(null)}>Quay lại danh sách</button></div></Modal>}
  </form>
}
