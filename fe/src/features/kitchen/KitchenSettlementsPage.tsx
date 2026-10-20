import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { isAxiosError } from 'axios'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { QueryState } from '../../components/QueryState'
import { FilterPanel } from '../../components/FilterPanel'
import { apiErrorMessage } from '../../lib/api'
import { schoolToday, schoolDateTime } from '../../lib/schoolTime'
import { searchKitchenSettlements, kitchenDiff, kitchenHistory, acknowledgeKitchen, type KitchenSummary } from './kitchenApi'

export function KitchenSettlementsPage() {
  const [date, setDate] = useState(schoolToday)
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<KitchenSummary | null>(null)
  const query = useQuery({ queryKey: ['kitchen-settlements', date, page], queryFn: () => searchKitchenSettlements(date, page), refetchInterval: 15000 })
  return <section>
    <h1>Tiếp nhận suất ăn</h1><p className="lead">Đối chiếu số suất hiện hành trước khi xác nhận tiếp nhận. Khi có bản điều chỉnh mới, bếp cần xác nhận lại.</p>
    <div className="panel workflow-lists"><div className="panel-head"><h2>Phiên ăn đã chốt</h2><button type="button" className="button secondary" disabled={query.isFetching} onClick={() => void query.refetch()}>Tải lại</button></div>
      <FilterPanel activeCount={date ? 1 : 0}><div className="list-toolbar"><label className="field">Ngày ăn<input type="date" value={date} onChange={e => { setDate(e.target.value); setPage(1) }} /></label><button type="button" className="button secondary" onClick={() => { setDate(''); setPage(1) }}>Tất cả ngày</button></div></FilterPanel>
      <QueryState loadingMessage="Đang tải…" loading={query.isPending} error={query.isError} errorMessage={apiErrorMessage(query.error)} onRetry={() => void query.refetch()}>
        <div className="table-wrap"><ResponsiveTable><thead><tr><th>Ngày / bữa</th><th>Suất hiện hành</th><th>Thay đổi</th><th>Tiếp nhận</th><th>Thao tác</th></tr></thead><tbody>
          {query.data?.items.map(item => <tr key={item.mealDayId}><td><strong>{item.date}</strong><small>{item.sessionType}</small></td><td><strong>{item.appliedPortions} suất</strong><small>Gốc: {item.originalPortions}</small></td><td>{item.diff > 0 ? '+' : ''}{item.diff}</td><td><span className={`status ${item.isAcknowledged ? 'active' : ''}`}>{item.isAcknowledged ? 'Đã tiếp nhận bản hiện hành' : 'Cần xác nhận'}</span></td><td><button type="button" className="button secondary" onClick={() => setSelected(item)}>Đối chiếu / tiếp nhận</button></td></tr>)}
          {!query.data?.items.length && <tr><td colSpan={5} className="empty">Chưa có phiên ăn đã chốt phù hợp.</td></tr>}
        </tbody></ResponsiveTable></div>
      </QueryState>
      {query.data && <Pagination page={page} pageSize={20} total={query.data.total} busy={query.isFetching} onChange={setPage} />}
    </div>
    {selected && <KitchenDetail key={selected.mealDayId} item={selected} onClose={() => setSelected(null)} />}
  </section>
}

function KitchenDetail({ item, onClose }: Readonly<{ item: KitchenSummary; onClose: () => void }>) {
  const cache = useQueryClient()
  const [reviewed, setReviewed] = useState(false)
  const [note, setNote] = useState('')
  const query = useQuery({ queryKey: ['kitchen-diff', item.mealDayId], queryFn: () => kitchenDiff(item.mealDayId), refetchOnWindowFocus: false, refetchOnReconnect: false })
  const history = useQuery({ queryKey: ['kitchen-history', item.mealDayId], queryFn: () => kitchenHistory(item.mealDayId) })
  async function reload() { setReviewed(false); await Promise.all([query.refetch(), history.refetch()]) }
  const mutation = useMutation({ mutationFn: () => acknowledgeKitchen(item.mealDayId, query.data!.snapshotToken, note),
    onSuccess: async () => { toast.success('Đã xác nhận tiếp nhận bản suất hiện hành.'); setReviewed(false); setNote(''); await cache.invalidateQueries({ queryKey: ['kitchen-settlements'] }); await reload() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); if (isAxiosError(error) && error.response?.status === 409) await reload() } })
  const canConfirm = reviewed && !!query.data && !query.data.isAcknowledged && !query.isFetching && !query.isError && !mutation.isPending
  function submit(event: FormEvent) { event.preventDefault(); if (canConfirm) mutation.mutate() }
  return <Modal wide title={`${item.sessionType} · ${item.date}`} busy={mutation.isPending} onClose={onClose}>
    <div className="workflow-form"><button type="button" className="button secondary" disabled={query.isFetching || mutation.isPending} onClick={() => void reload()}>Tải lại bản suất</button>
      <QueryState loadingMessage="Đang tải…" loading={query.isPending} error={query.isError} errorMessage={apiErrorMessage(query.error)} onRetry={() => void reload()}>
        {query.data && <><div className="amendment-headline"><span>Số suất cần chuẩn bị</span><strong>{query.data.appliedTotal} suất</strong><small>Gốc {query.data.originalTotal} · Chênh lệch {query.data.totalDiff > 0 ? '+' : ''}{query.data.totalDiff}</small></div>
          <div className="table-wrap"><ResponsiveTable><thead><tr><th>Lớp</th><th>Gốc → hiện hành</th><th>Phiên bản</th><th>Đối chiếu trẻ</th></tr></thead><tbody>{query.data.classDiffs.map(room => <tr key={room.classId}><td><strong>{room.className}</strong></td><td>{room.originalCount} → {room.appliedCount}<small>Điều chỉnh tổng suất: {room.kitchenAdjustment}</small></td><td>Bản {room.currentVersion}</td><td>{room.hasCompleteRoster ? <details><summary>+{room.addedStudents.length} / −{room.removedStudents.length} trẻ</summary><p>Thêm: {room.addedStudents.map(x => x.studentName).join(', ') || 'Không có'}</p><p>Bớt: {room.removedStudents.map(x => x.studentName).join(', ') || 'Không có'}</p></details> : <span>Chưa đủ dữ liệu lịch sử để đối chiếu trẻ</span>}</td></tr>)}</tbody></ResponsiveTable></div>
          {query.data.isAcknowledged ? <p role="status">Bếp đã tiếp nhận bản suất hiện hành.</p> : <form className="workflow-form" onSubmit={submit}><label className="switch-line"><input type="checkbox" checked={reviewed} disabled={mutation.isPending || query.isFetching} onChange={e => setReviewed(e.target.checked)} />Tôi đã đối chiếu số suất và thay đổi của bản đang hiển thị</label><label className="field">Ghi chú tiếp nhận<textarea maxLength={500} value={note} disabled={mutation.isPending} onChange={e => setNote(e.target.value)} /></label><button type="submit" className="button primary" disabled={!canConfirm}>{mutation.isPending ? 'Đang xác nhận…' : 'Xác nhận tiếp nhận'}</button></form>}
        </>}
      </QueryState>
      <details className="entry"><summary>Lịch sử tiếp nhận</summary><QueryState loadingMessage="Đang tải…" loading={history.isPending} error={history.isError} errorMessage={apiErrorMessage(history.error)} onRetry={() => void history.refetch()}>
        {history.data?.map(entry => <article className="entry" key={entry.id}><strong>{entry.portionsCount} suất · {entry.acknowledgedByName}</strong><small>{schoolDateTime(entry.acknowledgedAt)}</small><p>{entry.note || 'Không có ghi chú'}</p><small>{entry.snapshotToken === query.data?.snapshotToken ? 'Bản hiện hành' : 'Bản trước hoặc chưa có thông tin phiên bản'}</small></article>)}
        {!history.data?.length && <p>Chưa có lịch sử tiếp nhận.</p>}
      </QueryState></details>
    </div>
  </Modal>
}
