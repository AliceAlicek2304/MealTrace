import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'

type LinkRequest = { id: string; studentCode: string; studentName: string; relationship: string; note: string; status: string; requestedAt: string; reviewReason: string | null; revision: number; parentName: string; parentPhone: string | null; className: string | null; classId: string | null; revokedAt: string | null; revocationReason: string | null }
const statuses: Record<string, string> = { PENDING: 'Chờ duyệt', APPROVED: 'Đã duyệt', REJECTED: 'Từ chối', CANCELLED: 'Đã hủy', REVOKED: 'Đã thu hồi' }
const relationships: Record<string, string> = { FATHER: 'Cha', MOTHER: 'Mẹ', GUARDIAN: 'Người giám hộ' }
const sortSchoolYearsNewestFirst = (years: string[]) => years.sort((a, b) => b.localeCompare(a, 'vi', { numeric: true }))
const getModalTitle = (mode: NonNullable<ParentLinksPageState['modal']>) => {
  const titles = { bulk: 'Duyệt theo danh sách lớp', revoke: 'Thu hồi liên kết', create: 'Yêu cầu liên kết trẻ', cancel: 'Hủy yêu cầu', review: 'Đối chiếu và duyệt liên kết' }
  return titles[mode]
}
type ParentLinksPageState = { modal: 'create' | 'review' | 'cancel' | 'bulk' | 'revoke' | null }

export function ParentLinksPage({ roles }: Readonly<{ roles: string[] }>) {
  const parent = roles.includes('PARENT')
  const staff = roles.some(x => ['ADMIN', 'TEACHER'].includes(x))
  const [review, setReview] = useState(!parent)
  const [status, setStatus] = useState(!parent ? 'PENDING' : '')
  const [page, setPage] = useState(1)
  const [modal, setModal] = useState<'create' | 'review' | 'cancel' | 'bulk' | 'revoke' | null>(null)
  const [selected, setSelected] = useState<LinkRequest | null>(null)
  const [year, setYear] = useState('')
  const [formYear, setFormYear] = useState('')
  const [classId, setClassId] = useState('')
  const [checked, setChecked] = useState<Record<string, number>>({})
  const pageSize = review ? 100 : 25
  const classes = useQuery({ queryKey: ['parent-link-classes', review], enabled: staff || parent, queryFn: async () => (await api.get<{ id: string; name: string; schoolYear: string }[]>(review ? '/student-link-requests/classes' : '/parent/link-requests/classes')).data })
  const room = classes.data?.find(x => x.id === classId)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const cache = useQueryClient()
  const endpoint = review ? '/student-link-requests' : '/parent/link-requests'
  const query = useQuery({ queryKey: ['parent-links', review, status, page, classId, year], queryFn: async () => (await api.get<{ items: LinkRequest[]; total: number }>(endpoint, { params: { status: status || undefined, page, pageSize, classId: review ? classId || undefined : undefined, schoolYear: review ? year || undefined : undefined } })).data })

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) { return }
    const data = new FormData(event.currentTarget)
    setBusy(true); setError('')
    try {
      if (modal === 'create') await api.post('/parent/link-requests', { classId: data.get('classId'), studentCode: null, studentName: data.get('studentName'), relationship: data.get('relationship'), note: data.get('note') })
      else if (selected && modal === 'cancel') await api.post(`/parent/link-requests/${selected.id}/cancel`, { revision: selected.revision })
      else if (selected && modal === 'review') await api.post(`/student-link-requests/${selected.id}/review`, { revision: selected.revision, approve: data.get('approve') === 'true', reason: data.get('reason') })
      else if (modal === 'bulk') await api.post('/student-link-requests/bulk-review', { classId: classId || null, schoolYear: year || null, items: Object.entries(checked).map(([id, revision]) => ({ id, revision })), approve: data.get('approve') === 'true', reason: data.get('reason') })
      else if (selected && modal === 'revoke') await api.post(`/student-link-requests/${selected.id}/revoke`, { revision: selected.revision, reason: data.get('reason') })
      setChecked({})
      setModal(null)
      await cache.invalidateQueries()
    } catch (error_) { setError(apiErrorMessage(error_)) }
    finally { setBusy(false) }
  }
  function open(mode: 'create' | 'review' | 'cancel' | 'bulk' | 'revoke', item: LinkRequest | null = null) { setSelected(item); setError(''); setModal(mode) }

  return <section>
    <h1>Liên kết trẻ</h1>
    <p>Chọn năm học và lớp của trẻ, nhập họ tên trẻ và gửi yêu cầu để giáo viên đối chiếu. Chỉ sau khi được duyệt, phụ huynh mới có quyền xem và báo vắng cho trẻ.</p>
    {parent && staff && <div className="actions"><button type="button" className="button secondary" onClick={() => { setReview(false); setStatus(''); setPage(1); setChecked({}) }}>Yêu cầu của tôi</button><button type="button" className="button secondary" onClick={() => { setReview(true); setStatus('PENDING'); setPage(1); setChecked({}) }}>Cần nhà trường duyệt</button></div>}
    {!review && <button type="button" className="button primary" onClick={() => open('create')}>Yêu cầu liên kết trẻ</button>}
    {review && <div className="panel link-class-panel"><label className="field">Năm học<select value={year} onChange={e => { setYear(e.target.value); setClassId(''); setPage(1); setChecked({}) }}><option value="">Tất cả năm học</option>{sortSchoolYearsNewestFirst([...new Set(classes.data?.map(x => x.schoolYear) ?? [])]).map(x => <option key={x}>{x}</option>)}</select></label><label className="field">Lớp phụ trách<select value={classId} onChange={e => { setClassId(e.target.value); setPage(1); setChecked({}) }}><option value="">Tất cả lớp được phân công</option>{classes.data?.filter(x => !year || x.schoolYear === year).map(x => <option key={x.id} value={x.id}>{x.name} · {x.schoolYear}</option>)}</select></label>
      {classes.isError && <p role="alert">{apiErrorMessage(classes.error)} <button type="button" onClick={() => void classes.refetch()}>Thử lại</button></p>}
      <div className="actions"><button type="button" className="button secondary" disabled={(!classId && !year) || query.isFetching || busy} onClick={() => setChecked(Object.fromEntries((query.data?.items ?? []).filter(x => x.status === 'PENDING').map(x => [x.id, x.revision])))}>Chọn yêu cầu chờ trên trang</button><button type="button" className="button secondary" onClick={() => setChecked({})} disabled={busy}>Bỏ chọn</button><button type="button" className="button primary" disabled={(!classId && !year) || !Object.keys(checked).length || query.isFetching || busy} onClick={() => open('bulk')}>Xử lý {Object.keys(checked).length} yêu cầu đã chọn</button></div>
      <p>Rà danh sách trẻ, tên phụ huynh và SĐT; bỏ chọn trường hợp chưa rõ. Mỗi lượt xử lý tối đa 100 yêu cầu đã chọn.</p></div>}
    <label className="field">Trạng thái<select value={status} onChange={e => { setStatus(e.target.value); setPage(1); setChecked({}) }}><option value="">Tất cả</option>{Object.entries(statuses).map(([key, value]) => <option key={key} value={key}>{value}</option>)}</select></label>
    {query.isPending && <output aria-live="polite">Đang tải…</output>}
    {query.isError && <div role="alert">{apiErrorMessage(query.error)} <button type="button" onClick={() => void query.refetch()}>Thử lại</button></div>}
    {query.data?.items.length === 0 && <p>Chưa có yêu cầu liên kết.</p>}
    <div className="link-request-list">{query.data?.items.map(item => <article className="link-request-card" key={item.id}>
      {review && (classId || year) && item.status === 'PENDING' && <label className="link-selection"><input type="checkbox" aria-label={`Chọn ${item.studentName} - ${item.parentName}`} checked={Object.hasOwn(checked, item.id)} disabled={busy || query.isFetching} onChange={e => setChecked(previous => { const next = { ...previous }; if (e.target.checked) next[item.id] = item.revision; else delete next[item.id]; return next })} />Chọn yêu cầu</label>}
      <h2>{item.studentName}</h2><span className="badge">{statuses[item.status]}</span>
      <dl><dt>Mã trẻ</dt><dd>{item.studentCode}</dd><dt>Quan hệ</dt><dd>{relationships[item.relationship]}</dd>
        {review && <><dt>Phụ huynh</dt><dd>{item.parentName} · {item.parentPhone || 'Chưa có số điện thoại'}</dd><dt>Lớp</dt><dd>{item.className || 'Chưa có lớp hiện tại'}</dd></>}
        <dt>Ngày gửi</dt><dd>{new Date(item.requestedAt).toLocaleString('vi-VN')}</dd>
        {item.note && <><dt>Ghi chú</dt><dd>{item.note}</dd></>}{item.reviewReason && <><dt>Kết quả đối chiếu</dt><dd>{item.reviewReason}</dd></>}
        {item.revocationReason && <><dt>Lý do thu hồi</dt><dd>{item.revocationReason}</dd></>}
      </dl>
      {item.status === 'PENDING' && <button type="button" className="button secondary" onClick={() => open(review ? 'review' : 'cancel', item)}>{review ? 'Duyệt / từ chối' : 'Hủy yêu cầu'}</button>}
      {review && item.status === 'APPROVED' && <button type="button" className="button secondary" onClick={() => open('revoke', item)}>Thu hồi liên kết sai</button>}
    </article>)}</div>
    {query.data && <Pagination page={page} pageSize={pageSize} total={query.data.total} onChange={next => { setPage(next); setChecked({}) }} busy={query.isFetching} />}
    {modal && <Modal title={getModalTitle(modal)} busy={busy} onClose={() => setModal(null)}><form className="workflow-form" onSubmit={submit}>
      {selected && <p><strong>{selected.studentName}</strong> · {selected.studentCode}</p>}
      {modal === 'create' && <><label className="field">Năm học của trẻ<select required value={formYear} onChange={e => setFormYear(e.target.value)}><option value="">Chọn năm học</option>{sortSchoolYearsNewestFirst([...new Set(classes.data?.map(x => x.schoolYear) ?? [])]).map(x => <option key={x}>{x}</option>)}</select></label><label className="field">Lớp của trẻ<select name="classId" required key={formYear} defaultValue=""><option value="">Chọn lớp</option>{classes.data?.filter(x => x.schoolYear === formYear).map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label><label className="field">Họ tên trẻ<input name="studentName" required maxLength={150} /></label><label className="field">Quan hệ<select name="relationship" required>{Object.entries(relationships).map(([key, value]) => <option key={key} value={key}>{value}</option>)}</select></label><label className="field">Ghi chú để nhà trường đối chiếu<textarea name="note" maxLength={500} /></label></>}
      {(modal === 'review' || modal === 'bulk') && <><p>Đối chiếu danh tính và quan hệ phụ huynh {selected?.parentName} với hồ sơ nhà trường trước khi cấp quyền truy cập trẻ.</p><label className="field">Quyết định<select name="approve"><option value="false">Từ chối</option><option value="true">Duyệt liên kết</option></select></label><label className="field">Kết quả đối chiếu / lý do<textarea name="reason" required maxLength={500} /></label></>}
      {modal === 'bulk' && <><p><strong>{room?.name || year} · {Object.keys(checked).length} yêu cầu</strong>. Chỉ các yêu cầu bạn đã chọn được xử lý. Nếu danh sách vừa thay đổi, hệ thống yêu cầu tải lại toàn bộ lượt duyệt.</p><ul>{query.data?.items.filter(x => Object.hasOwn(checked, x.id)).map(x => <li key={x.id}>{x.studentName} — {x.parentName} — {x.parentPhone}</li>)}</ul></>}
      {modal === 'revoke' && <><p>Gỡ quyền truy cập trẻ của {selected?.parentName} và thu hồi phiên đăng nhập hiện có. Lịch sử duyệt được giữ; phụ huynh có thể gửi yêu cầu đúng để duyệt lại.</p><label className="field">Lý do thu hồi<textarea name="reason" required maxLength={500} /></label></>}
      {modal === 'cancel'  && <p>Hủy yêu cầu đang chờ duyệt này?</p>}
      {error && <p role="alert">{error}</p>}<button type="submit" className="button primary" disabled={busy}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button>
    </form></Modal>}
  </section>
}
