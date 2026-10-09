import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { ClassPicker } from '../../components/ClassPicker'
import { FilterPanel } from '../../components/FilterPanel'
import { Pagination } from '../../components/Pagination'
import { Modal } from '../../components/Modal'
import { displayBirth, displayGender } from './StudentProfileFields'

type Batch = { id: string; fileName: string; sheetName: string; className: string; schoolYear: string; startDate: string; created: number; importedAt: string; importedByName: string }
type Row = { row: number; fullName: string; dateOfBirth: string | null; gender: string | null }
const time = (value: string) => new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(new Date(value))

export function StudentImportHistory() {
  const [classId, setClassId] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string | null>(null)
  const [child, setChild] = useState<Row | null>(null)
  const history = useQuery({ queryKey: ['student-import-history', classId, page], queryFn: async () =>
    (await api.get<{ items: Batch[]; total: number }>('/admin/students/import/history', { params: { classId: classId || undefined, page } })).data })
  const detail = useQuery({ queryKey: ['student-import-history', selected], enabled: !!selected, queryFn: async () =>
    (await api.get<{ batch: Batch; rows: Row[] }>(`/admin/students/import/history/${selected}`)).data })
  let historyContent
  if (history.isPending) historyContent = <output aria-live="polite">Đang tải…</output>
  else if (history.isError) historyContent = <div role="alert"><p className="error">{apiErrorMessage(history.error)}</p><button type="button" className="button secondary" onClick={() => history.refetch()}>Thử lại</button></div>
  else if (!history.data.items.length) historyContent = <p className="empty compact">Chưa có lịch sử nhập phù hợp.</p>
  else historyContent = <ul className="import-history-list">{history.data.items.map(batch => <li key={batch.id}><button type="button" className="import-history-card" onClick={() => setSelected(batch.id)}>
    <strong>{batch.className} · {batch.schoolYear}</strong><span className="status active">{batch.created} trẻ</span>
    <span>{batch.fileName}</span><small>{time(batch.importedAt)} · {batch.importedByName}</small><span className="student-import-detail-link">Xem chi tiết ›</span>
  </button></li>)}</ul>
  let detailContent
  if (detail.isPending) detailContent = <output aria-live="polite">Đang tải…</output>
  else if (detail.isError) detailContent = <div role="alert"><p className="error">{apiErrorMessage(detail.error)}</p><button type="button" className="button secondary" onClick={() => detail.refetch()}>Thử lại</button></div>
  else detailContent = <>
    <strong>{detail.data.batch.className} · {detail.data.batch.schoolYear} · {detail.data.batch.created} trẻ</strong>
    <details><summary>Thông tin lần nhập</summary><dl className="student-import-detail"><div><dt>File</dt><dd>{detail.data.batch.fileName} · {detail.data.batch.sheetName}</dd></div><div><dt>Người nhập</dt><dd>{detail.data.batch.importedByName}</dd></div><div><dt>Thời gian</dt><dd>{time(detail.data.batch.importedAt)}</dd></div><div><dt>Ngày bắt đầu học</dt><dd>{displayBirth(detail.data.batch.startDate)}</dd></div></dl></details>
    <ul className="import-history-list">{detail.data.rows.map(row => <li key={row.row}><button type="button" className="import-history-card" onClick={() => setChild(row)}><strong>{row.fullName}</strong><small>Dòng {row.row}</small><span className="student-import-detail-link">Xem thông tin đã nhập ›</span></button></li>)}</ul>
  </>
  return <section className="workflow-form">
    <FilterPanel activeCount={classId ? 1 : 0}><ClassPicker compact showSchoolYear value={classId} onChange={value => { setClassId(value); setPage(1) }} label="Lọc lớp" /></FilterPanel>
    {historyContent}
    <Pagination page={page} total={history.data?.total ?? 0} pageSize={20} busy={history.isFetching} onChange={setPage} />
    {selected && <Modal title="Chi tiết lần nhập" onClose={() => { setSelected(null); setChild(null) }}>
      <section className="workflow-form">{detailContent}</section>
    </Modal>}
    {child && <Modal title="Thông tin trẻ lúc nhập" onClose={() => setChild(null)}><dl className="student-import-detail"><div><dt>Họ tên</dt><dd>{child.fullName}</dd></div><div><dt>Ngày sinh</dt><dd>{displayBirth(child.dateOfBirth)}</dd></div><div><dt>Giới tính</dt><dd>{displayGender(child.gender)}</dd></div></dl></Modal>}
  </section>
}
