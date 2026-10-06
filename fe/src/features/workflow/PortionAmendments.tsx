import { schoolDateTime } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'

type Child = { studentId: string; studentName: string }
type Candidate = Child & { studentCode: string; willEat: boolean; source: string }
type Snapshot = { id: string; version: number; count: number; kitchenAdjustment: number; students: Child[]; decisions: Candidate[] }
type Request = Child & { id: string; studentCode: string; baseSettlementId: string; baseVersion: number; wasEating: boolean; willEat: boolean;
  quantity: number; isQuantityOnly: boolean; students: (Child & { studentCode: string })[]; reason: string; requestedByName: string; requestedAt: string; status: 'PENDING' | 'APPROVED' | 'REJECTED';
  reviewReason: string | null; reviewedByName: string | null; reviewedAt: string | null }
type Result = { className: string; canRequest: boolean; original: Snapshot; current: Snapshot; hasOriginalSources: boolean; hasCompleteRoster: boolean;
  added: Child[]; removed: Child[]; candidates: Candidate[]; candidateTotal: number; items: Request[]; total: number }
type Room = { classId: string; className: string }
const statuses = { PENDING: 'Chờ xử lý', APPROVED: 'Đã duyệt', REJECTED: 'Đã từ chối' }
const sources: Record<string, string> = { DEFAULT: 'Mặc định', PARENT_ABSENCE: 'Phụ huynh báo không ăn', STAFF_EAT: 'Ngoại lệ có suất',
  STAFF_ABSENT: 'Ngoại lệ không có suất', APPROVED_AMENDMENT: 'Điều chỉnh đã duyệt', LEGACY_OR_CURRENT_ENROLLMENT: 'Nguồn cũ hoặc ghi danh theo ngày' }
const time = (value: string) => schoolDateTime(value)

function SnapshotView({ title, snapshot }: { title: string; snapshot: Snapshot }) {
  return <details className="entry"><summary>{title} · bản {snapshot.version} · {snapshot.count} suất</summary>
    <p>Số trẻ có suất: {snapshot.students.length} · Điều chỉnh riêng cho bếp: {snapshot.kitchenAdjustment > 0 ? "+" : ""}{snapshot.kitchenAdjustment}</p><p className="workflow-names">{snapshot.students.map(x => x.studentName).join(', ') || 'Không có trẻ ăn'}</p>
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
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [candidatePage, setCandidatePage] = useState(1)
  const [page, setPage] = useState(1)
  const [view, setView] = useState<'requests' | 'candidates'>('requests')
  const [snapshotDialog, setSnapshotDialog] = useState<{ title: string; snapshot: Snapshot } | null>(null)
  const [target, setTarget] = useState<{ kind: 'request'; children: Candidate[]; quantity: number; willEat: boolean; baseId: string; baseVersion: number; classId: string; beforeCount: number } | { kind: 'review'; request: Request } | null>(null)
  const [selected, setSelected] = useState<Candidate[]>([])
  const [selectedBase, setSelectedBase] = useState('')
  const [willEat, setWillEat] = useState(false)
  const [quantityOnly, setQuantityOnly] = useState(false)
  const [quantity, setQuantity] = useState(1)
  const [reason, setReason] = useState('')
  const endpoint = `/meal-days/${mealId}/amendments`
  const query = useQuery({ queryKey: ['portion-amendments', mealId, classId, searchTerm, page, candidatePage], enabled: !searchWaiting && !!classId,
    queryFn: async () => (await api.get<Result>(endpoint, { params: { classId, q: searchTerm, page, candidatePage } })).data, refetchInterval: 15000 })
  const detailId = target?.kind === 'review' ? target.request.id : ''
  const detail = useQuery({ queryKey: ['portion-amendment-detail', mealId, detailId], enabled: !!detailId,
    queryFn: async () => (await api.get<{ before: Snapshot; after: Snapshot | null }>(`${endpoint}/${detailId}`)).data })
  async function refresh() {
    await Promise.all([cache.invalidateQueries({ queryKey: ['portion-amendments', mealId] }),
      cache.invalidateQueries({ queryKey: ['portions', mealId] }), cache.invalidateQueries({ queryKey: ['meal-days'] }),
      cache.invalidateQueries({ queryKey: ['workflow-days'] }), cache.invalidateQueries({ queryKey: ['meal-day', mealId] }), cache.invalidateQueries({ queryKey: ['portion-amendment-detail', mealId] })])
  }
  const request = useMutation({ mutationFn: async () => {
    if (target?.kind !== 'request' || target.baseId !== data?.current.id) throw new Error('Chưa chọn phiếu.')
    return api.post(endpoint, { classId: target.classId, studentIds: target.children.map(x => x.studentId), quantity: target.quantity, baseSettlementId: target.baseId, willEat: target.willEat, reason })
  }, onSuccess: async () => { setTarget(null); setSelected([]); setSelectedBase(''); toast.success('Đã gửi yêu cầu. Số suất chỉ thay đổi khi Admin duyệt.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const review = useMutation({ mutationFn: async (approve: boolean) => {
    if (target?.kind !== 'review') throw new Error('Chưa chọn yêu cầu.')
    return api.post(`${endpoint}/${target.request.id}/review`, { approve, reason })
  }, onSuccess: async (_, approve) => { setTarget(null); toast.success(approve ? 'Đã duyệt và lưu bản suất mới cho bếp.' : 'Đã từ chối yêu cầu.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const busy = request.isPending || review.isPending
  const data = query.data
  const selectionStale = !!selected.length && selectedBase !== data?.current.id
  const eligibleInPage = data?.candidates.filter(child => child.willEat !== willEat) ?? []
  const selectedIds = new Set(selected.map(child => child.studentId))
  const unselectedInPage = eligibleInPage.filter(child => !selectedIds.has(child.studentId))
  const delta = willEat ? 1 : -1
  const previewCount = data ? data.current.count + delta * (quantityOnly ? quantity : selected.length) : 0
  const shownRequest = target?.kind === 'review' ? data?.items.find(x => x.id === target.request.id) ?? target.request : null
  const stale = target?.kind === 'request' ? target.baseId !== data?.current.id : shownRequest?.baseSettlementId !== data?.current.id

  if (!rooms.length) return <section className="panel"><p className="empty">Bản chốt cũ chưa có danh sách theo lớp; cần đối chiếu hồ sơ trước khi điều chỉnh.</p></section>

  return <section className="panel workflow-lists amendment-panel"><h2>Điều chỉnh sau chốt</h2>
    <p>Bản gốc giữ nguyên. Một phiếu chọn nhiều trẻ hoặc điều chỉnh số lượng bếp; chỉ bản được Admin duyệt mới áp dụng cho bếp.</p>
    <div className="list-toolbar"><label className="field">Lớp<select value={classId} onChange={event => { setPickedClass(event.target.value); setSelected([]); setSelectedBase(''); setSearch(''); setCandidatePage(1); setPage(1) }}>
      {rooms.map(room => <option key={room.classId} value={room.classId}>{room.className}</option>)}</select></label>
      {view === 'candidates' && <label className="field">Tìm trẻ<input placeholder="Tên hoặc mã trẻ" value={search} onChange={event => { setSearch(event.target.value); setCandidatePage(1) }} /></label>}
    </div>
    <SearchFeedback waiting={searchWaiting} fetching={query.isFetching} />
    {query.isPending ? <p className="empty">Đang tải bản suất…</p> : query.isError ? <p className="error">Không tải được điều chỉnh. <button type="button" className="button secondary" onClick={() => void query.refetch()}>Thử lại</button></p> : data && <>
      <div className="panel-head"><div><strong>{data.className}: gốc {data.original.count} → đang áp dụng {data.current.count} suất · bản {data.current.version}</strong>
        <p>Tăng {data.added.length} trẻ · Giảm {data.removed.length} trẻ · Điều chỉnh riêng cho bếp: {data.current.kitchenAdjustment > 0 ? "+" : ""}{data.current.kitchenAdjustment}</p></div><div className="table-actions"><button type="button" className="button secondary" onClick={() => setSnapshotDialog({ title: 'Bản gốc', snapshot: data.original })}>Xem bản gốc</button><button type="button" className="button secondary" onClick={() => setSnapshotDialog({ title: 'Đang áp dụng', snapshot: data.current })}>Xem bản hiện hành</button></div></div>
      {!data.hasOriginalSources && <p className="muted">Bản chốt cũ chưa lưu đầy đủ nguồn quyết định. Giữ danh sách gốc; không suy diễn lại lịch sử báo vắng.</p>}
      {!data.hasCompleteRoster && <p className="error">Bản cũ thiếu danh sách trẻ khớp tổng suất. Cần đối chiếu hồ sơ trước khi điều chỉnh theo trẻ.</p>}
      <div className="list-tabs modal-tabs"><button type="button" aria-pressed={view === 'requests'} onClick={() => setView('requests')}>Yêu cầu và lịch sử</button>{data.canRequest && <button type="button" aria-pressed={view === 'candidates'} onClick={() => setView('candidates')}>Tạo yêu cầu</button>}</div>
      {view === 'candidates' && data.canRequest && <>
        <div className="list-toolbar">
          <label className="field">Thao tác<select value={willEat ? 'add' : 'remove'} onChange={event => { setWillEat(event.target.value === 'add'); setSelected([]); setSelectedBase('') }}><option value="remove">Giảm suất</option><option value="add">Tăng suất</option></select></label>
          <label className="field">Loại phiếu<select value={quantityOnly ? 'quantity' : 'children'} onChange={event => { setQuantityOnly(event.target.value === 'quantity'); setSelected([]); setSelectedBase('') }}><option value="children">Chọn trẻ</option><option value="quantity">Chỉ điều chỉnh số lượng bếp</option></select></label>
          {quantityOnly && <label className="field">Số suất<input type="number" min={1} max={200} value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></label>}
          <button type="button" className="button primary" disabled={searchWaiting || query.isFetching || (!willEat && (quantityOnly ? quantity : selected.length) > data.current.count) || (quantityOnly ? !Number.isInteger(quantity) || quantity < 1 || quantity > 200 : !selected.length || selected.length > 200 || selectedBase !== data.current.id)} onClick={() => { setReason(''); setTarget({ kind: 'request', children: quantityOnly ? [] : [...selected], quantity: quantityOnly ? quantity : selected.length, willEat, baseId: data.current.id, baseVersion: data.current.version, classId, beforeCount: data.current.count }) }}>Tạo phiếu {willEat ? 'tăng' : 'giảm'} {quantityOnly ? quantity : selected.length} suất</button>
        </div>
        {!willEat && (quantityOnly ? quantity : selected.length) > data.current.count && <p className="error">Số suất giảm vượt tổng đang gửi bếp.</p>}
        <p><strong>Dự kiến gửi bếp: {data.current.count} → {Number.isInteger(previewCount) ? previewCount : "—"} suất</strong> (chỉ áp dụng sau khi duyệt).</p>
        {quantityOnly ? <p className="muted">Chỉ thay đổi số lượng gửi bếp. Không thay đổi trạng thái ăn hoặc tiền ăn của từng trẻ.</p> : <>
          <p>Đã chọn {selected.length} trẻ (giữ lựa chọn khi tìm kiếm/chuyển trang). <button type="button" className="button secondary" disabled={query.isFetching || searchWaiting || selectionStale || !unselectedInPage.length || selected.length + unselectedInPage.length > 200} onClick={() => { setSelectedBase(data.current.id); setSelected(previous => [...previous, ...unselectedInPage]) }}>Chọn trẻ phù hợp trong trang</button> <button type="button" className="button secondary" onClick={() => { setSelected([]); setSelectedBase('') }}>Bỏ chọn</button></p>
          {selected.length > 0 && <div className="selected-children" aria-label="Trẻ đã chọn">{selected.map(child => <button type="button" className="button secondary" key={child.studentId} aria-label={`Bỏ chọn ${child.studentName}`} onClick={() => setSelected(previous => previous.filter(x => x.studentId !== child.studentId))}>{child.studentName} ×</button>)}</div>}
          {selected.length > 0 && selectedBase !== data.current.id && <p className="error">Bản suất đã đổi. Bỏ chọn rồi đối chiếu lại danh sách.</p>}
          <div className="table-wrap"><table><thead><tr><th>Chọn</th><th>Trẻ</th><th>Bản đang áp dụng</th><th>Nguồn</th></tr></thead><tbody>
            {data.candidates.map(child => <tr key={child.studentId}><td><input type="checkbox" aria-label={`Chọn ${child.studentName}`} disabled={child.willEat === willEat || searchWaiting || query.isFetching || (selected.length >= 200 && !selected.some(x => x.studentId === child.studentId)) || (selected.length > 0 && selectedBase !== data.current.id)} checked={selected.some(x => x.studentId === child.studentId)} onChange={event => { setSelectedBase(data.current.id); setSelected(previous => event.target.checked ? [...previous, child] : previous.filter(x => x.studentId !== child.studentId)) }} /></td><td>{child.studentCode} · {child.studentName}</td><td>{child.willEat ? 'Có suất' : 'Không có suất'}</td><td>{sources[child.source] ?? child.source}</td></tr>)}
          </tbody></table></div>
          {!data.candidates.length && <p className="empty">Không có trẻ phù hợp.</p>}
          <Pagination page={candidatePage} total={data.candidateTotal} pageSize={25} busy={searchWaiting || query.isFetching} onChange={setCandidatePage} />
        </>}</>}
      {view === 'requests' && <><div className="table-wrap"><table><thead><tr><th scope="col">Trẻ / mã trẻ</th><th scope="col">Thay đổi</th><th scope="col">Trạng thái</th><th scope="col">Lý do</th><th scope="col">Người gửi / bản nguồn</th><th scope="col">Thao tác</th></tr></thead><tbody>
      {data.items.map(item => <tr key={item.id}><td><strong>{item.isQuantityOnly ? "Số lượng bếp" : item.students.length > 0 ? item.students.map(x => x.studentName).join(", ") : item.studentName}</strong><small>{item.isQuantityOnly ? "Không gắn trẻ" : item.students.length > 0 ? item.students.map(x => x.studentCode).join(", ") : item.studentCode}</small></td><td>{item.willEat ? '+' : '−'}{item.quantity} suất</td><td>{statuses[item.status]}</td>
        <td>{item.reason}{item.reviewedAt && <small>{item.reviewedByName} · {time(item.reviewedAt)}: {item.reviewReason}</small>}</td><td>{item.requestedByName}<small>{time(item.requestedAt)} · bản {item.baseVersion}</small></td>
        <td><button type="button" className="button secondary" onClick={() => { setReason(''); setTarget({ kind: 'review', request: item }) }}>{isAdmin && item.status === 'PENDING' ? 'Xem và xử lý' : 'Xem bản nguồn'}</button></td></tr>)}
      </tbody></table></div>
      {!data.items.length && <p className="empty">Chưa có yêu cầu điều chỉnh.</p>}
      <Pagination page={page} total={data.total} pageSize={25} busy={searchWaiting || query.isFetching} onChange={setPage} /></>}
    </>}
    {snapshotDialog && <Modal wide title={snapshotDialog.title} onClose={() => setSnapshotDialog(null)}><div className="workflow-form"><SnapshotView title={snapshotDialog.title} snapshot={snapshotDialog.snapshot} /></div></Modal>}
    {target && <Modal wide title={target.kind === 'request' ? 'Yêu cầu điều chỉnh suất' : 'Chi tiết yêu cầu điều chỉnh'} busy={busy} onClose={() => setTarget(null)}>
      {target.kind === 'request' ? <form className="workflow-form" onSubmit={event => { event.preventDefault(); if (!busy && !stale && reason.trim() && !query.isError) request.mutate() }}>
        <p><strong>Gửi bếp: {target.beforeCount} → {target.beforeCount + (target.willEat ? target.quantity : -target.quantity)} suất</strong> · Chờ Admin duyệt</p><p>{target.willEat ? 'Tăng' : 'Giảm'} {target.quantity} suất · bản nguồn {target.baseVersion}</p><p>{target.children.length ? target.children.map(x => `${x.studentCode} · ${x.studentName}`).join(', ') : 'Điều chỉnh số lượng bếp; không thay đổi trạng thái ăn hoặc tiền ăn từng trẻ.'}</p>
        {stale && <p className="error">Bản suất đã thay đổi. Đóng cửa sổ và đối chiếu lại trước khi gửi.</p>}
        <label className="field">Lý do<textarea required maxLength={500} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={busy} onClick={() => setTarget(null)}>Hủy</button>
          <button type="submit" className="button primary" disabled={busy || stale || !reason.trim() || query.isError}>Gửi yêu cầu</button></div>
      </form> : <div className="workflow-form"><p>{shownRequest?.isQuantityOnly ? 'Số lượng bếp' : shownRequest?.students.length ? shownRequest.students.map(x => x.studentName).join(', ') : shownRequest?.studentName} · {shownRequest?.willEat ? 'Tăng' : 'Giảm'} {shownRequest?.quantity} suất · {shownRequest && statuses[shownRequest.status]}</p>
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
