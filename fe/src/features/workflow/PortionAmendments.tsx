import { ResponsiveTable } from '../../components/ResponsiveTable'
import { schoolDateTime } from '../../lib/schoolTime'
import { SearchFeedback } from '../../components/SearchFeedback'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { QueryState } from '../../components/QueryState'
import { usePortionAmendments, statuses, sources, type PortionAmendmentModel, type PortionAmendmentProps, type Snapshot, type Result, type Request } from './usePortionAmendments'

const time = schoolDateTime
type ContentProps = Readonly<{ model: PortionAmendmentModel; data: Result }>

function requestStudentNames(item: Request): string {
  if (item.isQuantityOnly) return 'Số lượng bếp'
  if (item.students.length) return item.students.map(child => child.studentName).join(', ')
  return item.studentName
}

function requestStudentCodes(item: Request): string {
  if (item.isQuantityOnly) return 'Không gắn trẻ'
  if (item.students.length) return item.students.map(child => child.studentCode).join(', ')
  return item.studentCode
}

function SnapshotView({ title, snapshot }: Readonly<{ title: string; snapshot: Snapshot }>) {
  return <details className="entry"><summary>{title} · bản {snapshot.version} · {snapshot.count} suất</summary>
    <p>Số trẻ có suất: {snapshot.students.length} · Điều chỉnh riêng cho bếp: {snapshot.kitchenAdjustment > 0 ? "+" : ""}{snapshot.kitchenAdjustment}</p><p className="workflow-names">{snapshot.students.map(x => x.studentName).join(', ') || 'Không có trẻ ăn'}</p>
    {snapshot.decisions.length > 0 && <div className="table-wrap"><ResponsiveTable><thead><tr><th>Trẻ</th><th>Suất</th><th>Nguồn đã lưu</th></tr></thead>
      <tbody>{snapshot.decisions.map(x => <tr key={x.studentId}><td>{x.studentCode} · {x.studentName}</td><td>{x.willEat ? 'Có' : 'Không'}</td><td>{sources[x.source] ?? x.source}</td></tr>)}</tbody></ResponsiveTable></div>}
  </details>
}

export function PortionAmendments(props: PortionAmendmentProps) {
  const model = usePortionAmendments(props)
  const { rooms, classId, view, search, query, data, searchWaiting, snapshotDialog, setPickedClass, setSelected,
    setSelectedBase, setSearch, setCandidatePage, setPage, setSnapshotDialog } = model
  if (!rooms.length) return <section className="panel"><p className="empty">Bản chốt cũ chưa có danh sách theo lớp; cần đối chiếu hồ sơ trước khi điều chỉnh.</p></section>
  return <section className="panel workflow-lists amendment-panel">
    <div className="amendment-intro"><h2>Đối chiếu suất ăn</h2><p>Thay đổi chỉ áp dụng sau khi Admin duyệt.</p>
      <details><summary>Hướng dẫn điều chỉnh</summary><p>Bản gốc luôn được giữ lại. Chọn nhiều trẻ trong một phiếu hoặc điều chỉnh riêng số lượng gửi bếp. Phiếu đang chờ duyệt chưa thay đổi suất hiện hành.</p></details></div>
    <div className="list-toolbar"><label className="field">Lớp<select value={classId} onChange={event => { setPickedClass(event.target.value); setSelected([]); setSelectedBase(''); setSearch(''); setCandidatePage(1); setPage(1) }}>
      {rooms.map(room => <option key={room.classId} value={room.classId}>{room.className}</option>)}</select></label>
      {view === 'candidates' && <label className="field">Tìm trẻ<input placeholder="Tên hoặc mã trẻ" value={search} onChange={event => { setSearch(event.target.value); setCandidatePage(1) }} /></label>}
    </div>
    <SearchFeedback waiting={searchWaiting} fetching={query.isFetching} />
    <QueryState loading={query.isPending} error={query.isError} loadingMessage="Đang tải bản suất…" errorMessage="Không tải được điều chỉnh." onRetry={() => void query.refetch()}>
      {data && <AmendmentContent model={model} data={data} />}
    </QueryState>
    {snapshotDialog && <Modal wide title={snapshotDialog.title} onClose={() => setSnapshotDialog(null)}><div className="workflow-form"><SnapshotView title={snapshotDialog.title} snapshot={snapshotDialog.snapshot} /></div></Modal>}
    <AmendmentDialog model={model} />
  </section>
}

function AmendmentContent({ model, data }: ContentProps) {
  const { view, setView, setSnapshotDialog } = model
  return <>
      <div className="amendment-headline"><span>Đang gửi bếp</span><strong>{data.current.count} suất</strong><small>Bản {data.current.version}</small></div>
      <details className="amendment-comparison"><summary>Đối chiếu bản gốc và thay đổi</summary><div className="amendment-comparison-content">
      <div className="amendment-overview" aria-label="So sánh số suất">
        <article><span>Bản chốt gốc</span><strong>{data.original.count}<small>suất</small></strong><span>Bản {data.original.version}</span><button type="button" className="button secondary" onClick={() => setSnapshotDialog({ title: 'Bản gốc', snapshot: data.original })}>Xem bản gốc</button></article>
        <article className="amendment-current" aria-label="Suất hiện hành"><span>Đang áp dụng</span><strong>{data.current.count}<small>suất</small></strong><span>Bản {data.current.version}</span><button type="button" className="button secondary" onClick={() => setSnapshotDialog({ title: 'Đang áp dụng', snapshot: data.current })}>Xem bản hiện hành</button></article>
      </div>
      <dl className="amendment-deltas"><div><dt>Trẻ tăng</dt><dd>+{data.added.length}</dd></div><div><dt>Trẻ giảm</dt><dd>−{data.removed.length}</dd></div><div><dt>Suất riêng bếp</dt><dd>{data.current.kitchenAdjustment > 0 ? '+' : ''}{data.current.kitchenAdjustment}</dd></div></dl>
      </div></details>
      {!data.hasOriginalSources && <p className="muted">Bản chốt cũ chưa lưu đầy đủ nguồn quyết định. Giữ danh sách gốc; không suy diễn lại lịch sử báo vắng.</p>}
      {!data.hasCompleteRoster && <p className="error">Bản cũ thiếu danh sách trẻ khớp tổng suất. Cần đối chiếu hồ sơ trước khi điều chỉnh theo trẻ.</p>}
      <div className="list-tabs amendment-view-switch" aria-label="Yêu cầu điều chỉnh"><button type="button" aria-pressed={view === 'requests'} onClick={() => setView('requests')}>Yêu cầu và lịch sử</button>{data.canRequest && <button type="button" aria-pressed={view === 'candidates'} onClick={() => setView('candidates')}>Tạo yêu cầu</button>}</div>

    {view === 'candidates' && data.canRequest && <CandidateEditor model={model} data={data} />}
    {view === 'requests' && <RequestHistory model={model} data={data} />}
  </>
}

function CandidateEditor({ model, data }: ContentProps) {
  const { willEat, setWillEat, setSelected, setSelectedBase, quantityOnly, setQuantityOnly, quantity, setQuantity,
    searchWaiting, query, selectionInvalid, exceedsCurrentCount, createRequest, selectedQuantity, previewCount } = model
  return <>
        <div className="list-toolbar">
          <label className="field">Thao tác<select value={willEat ? 'add' : 'remove'} onChange={event => { setWillEat(event.target.value === 'add'); setSelected([]); setSelectedBase('') }}><option value="remove">Giảm suất</option><option value="add">Tăng suất</option></select></label>
          <label className="field">Loại phiếu<select value={quantityOnly ? 'quantity' : 'children'} onChange={event => { setQuantityOnly(event.target.value === 'quantity'); setSelected([]); setSelectedBase('') }}><option value="children">Chọn trẻ</option><option value="quantity">Chỉ điều chỉnh số lượng bếp</option></select></label>
          {quantityOnly && <label className="field">Số suất<input type="number" min={1} max={200} value={quantity} onChange={event => setQuantity(Number(event.target.value))} /></label>}
          <button type="button" className="button primary" disabled={searchWaiting || query.isFetching || exceedsCurrentCount || selectionInvalid} onClick={createRequest}>Tạo phiếu {willEat ? 'tăng' : 'giảm'} {selectedQuantity} suất</button>
        </div>
        {exceedsCurrentCount && <p className="error">Số suất giảm vượt tổng đang gửi bếp.</p>}
        <output className="amendment-preview" aria-label="Dự kiến gửi bếp"><span>Dự kiến gửi bếp</span><strong>{data.current.count} → {Number.isInteger(previewCount) ? previewCount : '—'} suất</strong><small>Chỉ áp dụng sau khi duyệt.</small></output>

    {quantityOnly ? <p className="muted">Chỉ thay đổi số lượng gửi bếp. Không thay đổi trạng thái ăn hoặc tiền ăn của từng trẻ.</p> : <CandidateSelection model={model} data={data} />}
  </>
}

function CandidateSelection({ model, data }: ContentProps) {
  const { selected, query, searchWaiting, selectionStale, unselectedInPage, setSelectedBase, setSelected, selectedBase, willEat, candidatePage, setCandidatePage } = model
  return <>
          <p>Đã chọn {selected.length} trẻ (giữ lựa chọn khi tìm kiếm/chuyển trang). <button type="button" className="button secondary" disabled={query.isFetching || searchWaiting || selectionStale || !unselectedInPage.length || selected.length + unselectedInPage.length > 200} onClick={() => { setSelectedBase(data.current.id); setSelected(previous => [...previous, ...unselectedInPage]) }}>Chọn trẻ phù hợp trong trang</button> <button type="button" className="button secondary" onClick={() => { setSelected([]); setSelectedBase('') }}>Bỏ chọn</button></p>
          {selected.length > 0 && <div className="selected-children" aria-label="Trẻ đã chọn">{selected.map(child => <button type="button" className="button secondary" key={child.studentId} aria-label={`Bỏ chọn ${child.studentName}`} onClick={() => setSelected(previous => previous.filter(x => x.studentId !== child.studentId))}>{child.studentName} ×</button>)}</div>}
          {selected.length > 0 && selectedBase !== data.current.id && <p className="error">Bản suất đã đổi. Bỏ chọn rồi đối chiếu lại danh sách.</p>}
          <div className="table-wrap"><ResponsiveTable><thead><tr><th>Chọn</th><th>Trẻ</th><th>Bản đang áp dụng</th><th>Nguồn</th></tr></thead><tbody>
            {data.candidates.map(child => <tr key={child.studentId}><td><input type="checkbox" aria-label={`Chọn ${child.studentName}`} disabled={child.willEat === willEat || searchWaiting || query.isFetching || (selected.length >= 200 && !selected.some(x => x.studentId === child.studentId)) || (selected.length > 0 && selectedBase !== data.current.id)} checked={selected.some(x => x.studentId === child.studentId)} onChange={event => { setSelectedBase(data.current.id); setSelected(previous => event.target.checked ? [...previous, child] : previous.filter(x => x.studentId !== child.studentId)) }} /></td><td>{child.studentCode} · {child.studentName}</td><td>{child.willEat ? 'Có suất' : 'Không có suất'}</td><td>{sources[child.source] ?? child.source}</td></tr>)}
          </tbody></ResponsiveTable></div>
          {!data.candidates.length && <p className="empty">Không có trẻ phù hợp.</p>}
          <Pagination page={candidatePage} total={data.candidateTotal} pageSize={25} busy={searchWaiting || query.isFetching} onChange={setCandidatePage} />

  </>
}

function RequestHistory({ model, data }: ContentProps) {
  const { isAdmin, setReason, setTarget, page, searchWaiting, query, setPage } = model
  return <>
<div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Trẻ / mã trẻ</th><th scope="col">Thay đổi</th><th scope="col">Trạng thái</th><th scope="col">Lý do</th><th scope="col">Người gửi / bản nguồn</th><th scope="col">Thao tác</th></tr></thead><tbody>
      {data.items.map(item => <tr key={item.id}><td><strong>{requestStudentNames(item)}</strong><small>{requestStudentCodes(item)}</small></td><td>{item.willEat ? '+' : '−'}{item.quantity} suất</td><td>{statuses[item.status]}</td>
        <td>{item.reason}{item.reviewedAt && <small>{item.reviewedByName} · {time(item.reviewedAt)}: {item.reviewReason}</small>}</td><td>{item.requestedByName}<small>{time(item.requestedAt)} · bản {item.baseVersion}</small></td>
        <td><button type="button" className="button secondary" onClick={() => { setReason(''); setTarget({ kind: 'review', request: item }) }}>{isAdmin && item.status === 'PENDING' ? 'Xem và xử lý' : 'Xem bản nguồn'}</button></td></tr>)}
      </tbody></ResponsiveTable></div>
      {!data.items.length && <p className="empty">Chưa có yêu cầu điều chỉnh.</p>}
      <Pagination page={page} total={data.total} pageSize={25} busy={searchWaiting || query.isFetching} onChange={setPage} />
  </>
}

function AmendmentDialog({ model }: Readonly<{ model: PortionAmendmentModel }>) {
  const { target, busy, setTarget } = model
  if (!target) return null
  return <Modal wide title={target.kind === 'request' ? 'Yêu cầu điều chỉnh suất' : 'Chi tiết yêu cầu điều chỉnh'} busy={busy} onClose={() => setTarget(null)}>
    {target.kind === 'request' ? <RequestForm model={model} target={target} /> : <ReviewForm model={model} />}
  </Modal>
}

function RequestForm({ model, target }: Readonly<{ model: PortionAmendmentModel; target: Extract<NonNullable<PortionAmendmentModel['target']>, { kind: 'request' }> }>) {
  const { busy, stale, reason, query, request, setReason, setTarget } = model
  return <form className="workflow-form" onSubmit={event => { event.preventDefault(); if (!busy && !stale && reason.trim() && !query.isError) request.mutate() }}>
        <p><strong>Gửi bếp: {target.beforeCount} → {target.beforeCount + (target.willEat ? target.quantity : -target.quantity)} suất</strong> · Chờ Admin duyệt</p><p>{target.willEat ? 'Tăng' : 'Giảm'} {target.quantity} suất · bản nguồn {target.baseVersion}</p><p>{target.children.length ? target.children.map(x => `${x.studentCode} · ${x.studentName}`).join(', ') : 'Điều chỉnh số lượng bếp; không thay đổi trạng thái ăn hoặc tiền ăn từng trẻ.'}</p>
        {stale && <p className="error">Bản suất đã thay đổi. Đóng cửa sổ và đối chiếu lại trước khi gửi.</p>}
        <label className="field">Lý do<textarea required maxLength={500} value={reason} disabled={busy} onChange={event => setReason(event.target.value)} /></label>
        <div className="form-actions"><button type="button" className="button secondary" disabled={busy} onClick={() => setTarget(null)}>Hủy</button>
          <button type="submit" className="button primary" disabled={busy || stale || !reason.trim() || query.isError}>Gửi yêu cầu</button></div>
      </form>
}

function ReviewForm({ model }: Readonly<{ model: PortionAmendmentModel }>) {
  const { shownRequest, detail, isAdmin, stale, busy, reason, setReason, review, query } = model
  return <div className="workflow-form"><p>{shownRequest && requestStudentNames(shownRequest)} · {shownRequest?.willEat ? 'Tăng' : 'Giảm'} {shownRequest?.quantity} suất · {shownRequest && statuses[shownRequest.status]}</p>
        <p>Lý do đề nghị: {shownRequest?.reason}</p>
        <QueryState loading={detail.isPending} error={detail.isError} loadingMessage="Đang tải bản nguồn…" errorMessage="Không tải được bản nguồn.">{detail.data && <>
          <SnapshotView title="Nguồn yêu cầu" snapshot={detail.data.before} />
          {detail.data.after && <SnapshotView title="Bản sau duyệt" snapshot={detail.data.after} />}</>}</QueryState>
        {isAdmin && shownRequest?.status === 'PENDING' && <>
          {stale && <p className="error">Nguồn đã lỗi thời. Từ chối và yêu cầu gửi lại sau khi đối chiếu bản hiện hành.</p>}
          <label className="field">Lý do duyệt hoặc từ chối<textarea required maxLength={500} disabled={busy} value={reason} onChange={event => setReason(event.target.value)} /></label>
          <div className="form-actions"><button type="button" className="button secondary" disabled={busy || !reason.trim()} onClick={() => review.mutate(false)}>Từ chối</button>
            <button type="button" className="button primary" disabled={busy || stale || !reason.trim() || !detail.data || query.isError} onClick={() => review.mutate(true)}>Duyệt và áp dụng</button></div></>}
      </div>
}
