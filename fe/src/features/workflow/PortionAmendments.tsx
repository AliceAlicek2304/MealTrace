import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'

type Child = { studentId: string; studentName: string }
type Candidate = Child & { studentCode: string; willEat: boolean; source: string }
type Snapshot = { id: string; version: number; count: number; students: Child[]; decisions: Candidate[] }
type Request = Child & { id: string; studentCode: string; baseSettlementId: string; baseVersion: number; wasEating: boolean; willEat: boolean;
  reason: string; requestedByName: string; requestedAt: string; status: 'PENDING' | 'APPROVED' | 'REJECTED';
  reviewReason: string | null; reviewedByName: string | null; reviewedAt: string | null }
type Result = { className: string; canRequest: boolean; original: Snapshot; current: Snapshot; hasOriginalSources: boolean; hasCompleteRoster: boolean;
  added: Child[]; removed: Child[]; candidates: Candidate[]; candidateTotal: number; items: Request[]; total: number }
type Room = { classId: string; className: string }
const statuses = { PENDING: 'Chờ xử lý', APPROVED: 'Đã duyệt', REJECTED: 'Đã từ chối' }
const sources: Record<string, string> = { DEFAULT: 'Mặc định', PARENT_ABSENCE: 'Phụ huynh báo không ăn', STAFF_EAT: 'Ngoại lệ có suất',
  STAFF_ABSENT: 'Ngoại lệ không có suất', APPROVED_AMENDMENT: 'Điều chỉnh đã duyệt', LEGACY_OR_CURRENT_ENROLLMENT: 'Nguồn cũ hoặc ghi danh theo ngày' }
const time = (value: string) => new Date(value).toLocaleString('vi-VN')

function SnapshotView({ title, snapshot }: { title: string; snapshot: Snapshot }) {
  return <details className="entry"><summary>{title} · bản {snapshot.version} · {snapshot.count} suất</summary>
    <p className="workflow-names">{snapshot.students.map(x => x.studentName).join(', ') || 'Không có trẻ ăn'}</p>
    {snapshot.decisions.length > 0 && <div className="table-wrap"><table><thead><tr><th>Trẻ</th><th>Suất</th><th>Nguồn đã lưu</th></tr></thead>
      <tbody>{snapshot.decisions.map(x => <tr key={x.studentId}><td>{x.studentCode} · {x.studentName}</td><td>{x.willEat ? 'Có' : 'Không'}</td><td>{sources[x.source] ?? x.source}</td></tr>)}</tbody></table></div>}
  </details>
}

export function PortionAmendments({ mealId, rooms, roles }: { mealId: string; rooms: Room[]; roles: string[] }) {
  const cache = useQueryClient()
  const isAdmin = roles.includes('ADMIN')
  const [pickedClass, setPickedClass] = useState('')
  const classId = rooms.some(x => x.classId === pickedClass) ? pickedClass : rooms[0]?.classId ?? ''
  const [search, setSearch] = useState('')
  const [candidatePage, setCandidatePage] = useState(1)
  const [page, setPage] = useState(1)
  const [target, setTarget] = useState<{ kind: 'request'; child: Candidate; baseId: string; baseVersion: number; classId: string } | { kind: 'review'; request: Request } | null>(null)
  const [reason, setReason] = useState('')
  const endpoint = `/meal-days/${mealId}/amendments`
  const query = useQuery({ queryKey: ['portion-amendments', mealId, classId, search, page, candidatePage], enabled: !!classId,
    queryFn: async () => (await api.get<Result>(endpoint, { params: { classId, q: search, page, candidatePage } })).data, refetchInterval: 15000 })
  const detailId = target?.kind === 'review' ? target.request.id : ''
  const detail = useQuery({ queryKey: ['portion-amendment-detail', mealId, detailId], enabled: !!detailId,
    queryFn: async () => (await api.get<{ before: Snapshot; after: Snapshot | null }>(`${endpoint}/${detailId}`)).data })
  async function refresh() {
    await Promise.all([cache.invalidateQueries({ queryKey: ['portion-amendments', mealId] }),
      cache.invalidateQueries({ queryKey: ['portions', mealId] }), cache.invalidateQueries({ queryKey: ['meal-days'] }),
      cache.invalidateQueries({ queryKey: ['meal-day', mealId] }), cache.invalidateQueries({ queryKey: ['portion-amendment-detail', mealId] })])
  }
  const request = useMutation({ mutationFn: async () => {
    if (target?.kind !== 'request') throw new Error('Chưa chọn trẻ.')
    return api.post(endpoint, { classId: target.classId, studentId: target.child.studentId, baseSettlementId: target.baseId, willEat: !target.child.willEat, reason })
  }, onSuccess: async () => { setTarget(null); toast.success('Đã gửi yêu cầu. Số suất chỉ thay đổi khi Admin duyệt.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const review = useMutation({ mutationFn: async (approve: boolean) => {
    if (target?.kind !== 'review') throw new Error('Chưa chọn yêu cầu.')
    return api.post(`${endpoint}/${target.request.id}/review`, { approve, reason })
  }, onSuccess: async (_, approve) => { setTarget(null); toast.success(approve ? 'Đã duyệt và lưu bản suất mới cho bếp.' : 'Đã từ chối yêu cầu.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const busy = request.isPending || review.isPending
  const data = query.data
  const shownRequest = target?.kind === 'review' ? data?.items.find(x => x.id === target.request.id) ?? target.request : null
  const stale = target?.kind === 'request' ? target.baseId !== data?.current.id : shownRequest?.baseSettlementId !== data?.current.id

  if (!rooms.length) return <section className="panel"><p className="empty">Bản chốt cũ chưa có danh sách theo lớp; cần đối chiếu hồ sơ trước khi điều chỉnh.</p></section>

  return <section className="panel workflow-lists amendment-panel"><h2>Điều chỉnh sau chốt</h2>
    <p>Bản gốc giữ nguyên. Mỗi yêu cầu điều chỉnh một trẻ; chỉ bản được Admin duyệt mới áp dụng cho bếp.</p>
    <label className="field">Lớp<select value={classId} onChange={event => { setPickedClass(event.target.value); setSearch(''); setCandidatePage(1); setPage(1) }}>
      {rooms.map(room => <option key={room.classId} value={room.classId}>{room.className}</option>)}</select></label>
    {query.isPending ? <p className="empty">Đang tải bản suất…</p> : query.isError ? <p className="error">Không tải được điều chỉnh. <button type="button" className="button secondary" onClick={() => void query.refetch()}>Thử lại</button></p> : data && <>
      <div className="entry"><strong>{data.className}: gốc {data.original.count} → đang áp dụng {data.current.count} suất · bản {data.current.version}</strong>
        <p>Tăng: {data.added.map(x => x.studentName).join(', ') || 'Không'} · Giảm: {data.removed.map(x => x.studentName).join(', ') || 'Không'}</p></div>
      {!data.hasOriginalSources && <p className="muted">Bản chốt cũ chưa lưu đầy đủ nguồn quyết định. Giữ danh sách gốc; không suy diễn lại lịch sử báo vắng.</p>}
      {!data.hasCompleteRoster && <p className="error">Bản cũ thiếu danh sách trẻ khớp tổng suất. Cần đối chiếu hồ sơ trước khi điều chỉnh theo trẻ.</p>}
      <SnapshotView title="Bản gốc" snapshot={data.original} />
      {data.current.id !== data.original.id && <SnapshotView title="Đang áp dụng" snapshot={data.current} />}
      {data.canRequest && <><h3>Gửi yêu cầu cho trẻ</h3><label className="field">Tìm trẻ theo tên hoặc mã<input value={search} onChange={event => { setSearch(event.target.value); setCandidatePage(1) }} /></label>
        <div className="table-wrap"><table><thead><tr><th>Trẻ</th><th>Bản đang áp dụng</th><th>Nguồn</th><th>Thao tác</th></tr></thead><tbody>
          {data.candidates.map(child => <tr key={child.studentId}><td>{child.studentCode} · {child.studentName}</td><td>{child.willEat ? 'Có suất' : 'Không có suất'}</td><td>{sources[child.source] ?? child.source}</td>
            <td><button type="button" className="button secondary" onClick={() => { setReason(''); setTarget({ kind: 'request', child, baseId: data.current.id, baseVersion: data.current.version, classId }) }}>Yêu cầu {child.willEat ? 'giảm' : 'thêm'} suất</button></td></tr>)}</tbody></table></div>
        {!data.candidates.length && <p className="empty">Không có trẻ phù hợp.</p>}
        <Pagination page={candidatePage} total={data.candidateTotal} pageSize={25} busy={query.isFetching} onChange={setCandidatePage} /></>}
      <h3>Yêu cầu và lịch sử xử lý</h3>
      {data.items.map(item => <div className="entry" key={item.id}><strong>{item.studentCode} · {item.studentName} · {item.willEat ? '+1' : '−1'} suất · {statuses[item.status]}</strong>
        <p>{item.reason}</p><small>{item.requestedByName} · {time(item.requestedAt)} · nguồn bản {item.baseVersion}</small>
        {item.reviewedAt && <p>{item.reviewedByName} · {time(item.reviewedAt)}: {item.reviewReason}</p>}
        <button type="button" className="button secondary" onClick={() => { setReason(''); setTarget({ kind: 'review', request: item }) }}>{isAdmin && item.status === 'PENDING' ? 'Xem và xử lý' : 'Xem bản nguồn'}</button></div>)}
      {!data.items.length && <p className="empty">Chưa có yêu cầu điều chỉnh.</p>}
      <Pagination page={page} total={data.total} pageSize={25} busy={query.isFetching} onChange={setPage} />
    </>}
    {target && <Modal wide title={target.kind === 'request' ? 'Yêu cầu điều chỉnh suất' : 'Chi tiết yêu cầu điều chỉnh'} busy={busy} onClose={() => setTarget(null)}>
      {target.kind === 'request' ? <form className="workflow-form" onSubmit={event => { event.preventDefault(); request.mutate() }}>
        <p>{target.child.studentName} · bản nguồn {target.baseVersion}: {target.child.willEat ? 'Có suất → Không có suất' : 'Không có suất → Có suất'}</p>
        {stale && <p className="error">Bản suất đã thay đổi. Đóng cửa sổ và đối chiếu lại trước khi gửi.</p>}
        <label className="field">Lý do<textarea required maxLength={500} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={busy} onClick={() => setTarget(null)}>Hủy</button>
          <button type="submit" className="button primary" disabled={busy || stale || !reason.trim() || query.isError}>Gửi yêu cầu</button></div>
      </form> : <div className="workflow-form"><p>{shownRequest?.studentName} · {shownRequest?.willEat ? 'Thêm 1 suất' : 'Giảm 1 suất'} · {shownRequest && statuses[shownRequest.status]}</p>
        <p>Lý do đề nghị: {shownRequest?.reason}</p>
        {detail.isPending ? <p>Đang tải bản nguồn…</p> : detail.isError ? <p className="error">Không tải được bản nguồn.</p> : detail.data && <>
          <SnapshotView title="Nguồn yêu cầu" snapshot={detail.data.before} />
          {detail.data.after && <SnapshotView title="Bản sau duyệt" snapshot={detail.data.after} />}</>}
        {isAdmin && shownRequest?.status === 'PENDING' && <>
          {stale && <p className="error">Nguồn đã lỗi thời. Từ chối và yêu cầu gửi lại sau khi đối chiếu bản hiện hành.</p>}
          <label className="field">Lý do duyệt hoặc từ chối<textarea required maxLength={500} disabled={busy} value={reason} onChange={event => setReason(event.target.value)} /></label>
          <div className="form-actions"><button type="button" className="button secondary" disabled={busy || !reason.trim()} onClick={() => review.mutate(false)}>Từ chối</button>
            <button type="button" className="button primary" disabled={busy || stale || !reason.trim() || !detail.data || query.isError} onClick={() => review.mutate(true)}>Duyệt và áp dụng</button></div></>}
      </div>}
    </Modal>}
  </section>
}
