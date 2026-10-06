import { useDeadline } from '../../lib/useDeadline'
import { schoolDateTime } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { SearchFeedback } from '../../components/SearchFeedback'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { currentStudentPortion, type CurrentClassPortion } from './currentPortion'

type Decision = { studentId: string; studentCode: string; fullName: string; classId: string; className: string;
  willEat: boolean; source: string; parentReportedAbsent: boolean; latestEventId: string | null; latestAction: string | null; latestReason: string | null }
type Decisions = { items: Decision[]; total: number; canEdit: boolean; isSettled: boolean; isCancelled: boolean; cancellationReason: string | null; cutoffAt: string;
  classes: { id: string; name: string }[] }
type Action = 'EAT' | 'ABSENT' | 'DEFAULT'
type Event = { id: string; action: Action; reason: string | null; recordedAt: string; actorName: string | null; isLegacy: boolean; sequence: number }
type History = { items: Event[]; total: number }
const sourceLabel: Record<string, string> = { DEFAULT: 'Mặc định có suất', PARENT_ABSENCE: 'Phụ huynh đăng ký không ăn / báo vắng', STAFF_EAT: 'Ngoại lệ: có suất', STAFF_ABSENT: 'Ngoại lệ: không có suất' }
const actionLabel: Record<Action, string> = { EAT: 'Dự kiến có suất', ABSENT: 'Dự kiến không có suất', DEFAULT: 'Khôi phục mặc định' }

export function MealExceptions({ mealId }: { mealId: string }) {
  const cache = useQueryClient()
  const [search, setSearch] = useState('')
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [classId, setClassId] = useState('')
  const [page, setPage] = useState(1)
  const [target, setTarget] = useState<{ kind: 'edit' | 'history'; student: Decision } | null>(null)
  const [action, setAction] = useState<Action>('ABSENT')
  const [reason, setReason] = useState('')
  const [historyPage, setHistoryPage] = useState(1)
  const decisions = useQuery({ queryKey: ['meal-decisions', mealId, searchTerm, classId, page], enabled: !searchWaiting, refetchInterval: 15000,
    queryFn: async () => (await api.get<Decisions>(`/meal-days/${mealId}/decisions`, { params: { search: searchTerm, classId: classId || undefined, page } })).data })
  const applied = useQuery({ queryKey: ['portions', mealId], enabled: !!decisions.data?.isSettled, refetchInterval: 15000,
    queryFn: async () => (await api.get<{ classes: CurrentClassPortion[] }>(`/meal-days/${mealId}/portions`)).data })
  const appliedRooms = new Map(applied.data?.classes.map(room => [room.classId, room]))
  const filteredRooms = applied.data?.classes.filter(room => !classId || room.classId === classId)
  const history = useQuery({ queryKey: ['meal-exception-history', mealId, target?.student.studentId, historyPage], enabled: !!target,
    queryFn: async () => (await api.get<History>(`/meal-days/${mealId}/students/${target!.student.studentId}/exceptions`, { params: { page: historyPage } })).data })
  const cutoffPassed = useDeadline(decisions.data?.cutoffAt)
  const canEdit = !!decisions.data?.canEdit && Number.isFinite(Date.parse(decisions.data?.cutoffAt ?? '')) && !cutoffPassed
  const save = useMutation({ mutationFn: () => api.post(`/meal-days/${mealId}/exceptions`, {
    studentId: target!.student.studentId, action, reason, expectedEventId: target!.student.latestEventId,
  }), onSuccess: async () => {
    setTarget(null); setReason(''); toast.success('Đã cập nhật ngoại lệ và lưu lịch sử.')
    await Promise.all([cache.invalidateQueries({ queryKey: ['meal-decisions', mealId] }), cache.invalidateQueries({ queryKey: ['portions', mealId] }),
      cache.invalidateQueries({ queryKey: ['meal-exception-history', mealId] })])
  }, onError: async error => {
    toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' })
    await cache.invalidateQueries({ queryKey: ['meal-decisions', mealId] })
  } })
  function open(student: Decision, kind: 'edit' | 'history') {
    setTarget({ student, kind }); setHistoryPage(1); setReason(''); setAction(student.willEat ? 'ABSENT' : 'EAT')
  }
  const historyContent = <>
    {history.isPending ? <p>Đang tải lịch sử…</p> : history.isError ? <p className="error">{apiErrorMessage(history.error)}</p> : !history.data?.items.length ? <p className="form-help">Chưa có ngoại lệ.</p> :
      history.data.items.map(event => <div className="entry" key={event.id}><strong>{actionLabel[event.action]}</strong>
        <small>{event.actorName || (event.isLegacy ? 'Dữ liệu cũ: chưa ghi người xử lý' : 'Chưa có tên người xử lý')} · {schoolDateTime(event.recordedAt)}</small>
        <p>{event.reason || 'Chưa ghi lý do'}</p></div>)}
    <Pagination page={historyPage} total={history.data?.total ?? 0} pageSize={25} busy={history.isFetching || save.isPending} onChange={setHistoryPage} />
  </>
  return <section className="panel workflow-lists"><div className="panel-head"><div><h2>{decisions.data?.isSettled ? 'Nguồn trước điều chỉnh và suất hiện hành' : 'Nguồn dự kiến ăn và ngoại lệ'}</h2>
    <p>{decisions.data?.isSettled ? 'Cột tại giờ chốt giữ nguồn dự kiến ban đầu. Cột hiện hành lấy bản chốt mới nhất sau các phiếu đã duyệt; phiếu đang chờ chưa áp dụng.' : 'Bao gồm trẻ có suất và không có suất. Đây là dự kiến ăn, chưa xác nhận có mặt thực tế.'}</p></div></div>
    <div className="workflow-form workflow-fields"><label className="field">Tìm trẻ<input placeholder="Mã hoặc tên trẻ" value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
      <label className="field">Lớp<select value={classId} onChange={e => { setClassId(e.target.value); setPage(1) }}><option value="">Tất cả lớp được xem</option>
        {decisions.data?.classes.map(room => <option key={room.id} value={room.id}>{room.name}</option>)}</select></label></div>
    <SearchFeedback waiting={searchWaiting} fetching={decisions.isFetching} />
    {decisions.isPending ? <p className="empty compact">Đang tải…</p> : decisions.isError ? <p className="empty compact error">{apiErrorMessage(decisions.error)}</p> : <>
      {!canEdit && <p className="empty compact">{decisions.data?.isCancelled ? `Phiên đã hủy: ${decisions.data.cancellationReason ?? 'Theo lịch trường'}` : 'Đã qua giờ chốt hoặc phiên đã chốt; chỉ xem thông tin và lịch sử.'}</p>}
      {decisions.data?.isSettled && <div className="workflow-form">
        {applied.isPending ? <p>Đang tải suất hiện hành…</p> : applied.isError ? <p className="error">Không tải được suất hiện hành. <button type="button" className="button secondary" onClick={() => void applied.refetch()}>Thử lại</button></p> : <>
          <p><strong>Đang gửi bếp: {filteredRooms?.reduce((sum, room) => sum + (room.count ?? room.studentIds.length), 0) ?? 0} suất</strong> · {classId ? 'Lớp đã chọn' : 'Các lớp được xem'} (không phụ thuộc ô tìm trẻ).</p>
          {filteredRooms?.some(room => room.kitchenAdjustment !== 0) && <p>Điều chỉnh số lượng bếp không gắn trẻ: {filteredRooms.reduce((sum, room) => sum + room.kitchenAdjustment, 0)} suất. Trạng thái suất từng trẻ giữ riêng, không tự thay đổi tiền ăn.</p>}
        </>}
      </div>}
      {!decisions.data?.items.length && <p className="empty compact">{searchTerm || classId ? 'Không có trẻ phù hợp bộ lọc.' : 'Chưa có trẻ ghi danh tại ngày ăn.'}</p>}
      <div className="table-wrap"><table><thead><tr><th scope="col">Trẻ / mã trẻ</th><th scope="col">Lớp</th><th scope="col">{decisions.data?.isSettled ? 'Suất tại giờ chốt' : 'Suất dự kiến'}</th>{decisions.data?.isSettled && <th scope="col">Suất hiện hành</th>}<th scope="col">{decisions.data?.isSettled ? 'Nguồn tại giờ chốt / lý do' : 'Nguồn / lý do'}</th><th scope="col">Thao tác</th></tr></thead><tbody>
      {decisions.data?.items.map(student => {
        const room = appliedRooms.get(student.classId)
        const current = currentStudentPortion(student.studentId, room)
        return <tr key={student.studentId}><td><strong>{student.fullName}</strong><small>{student.studentCode}</small></td><td>{student.className}</td><td>{student.willEat ? 'Có suất' : 'Không có suất'}</td>{decisions.data.isSettled && <td>{applied.isPending ? 'Đang tải…' : applied.isError ? 'Không tải được' : current === null ? 'Chưa đủ dữ liệu' : <>
          <strong>{current ? 'Có suất' : 'Không có suất'}</strong><small>Bản {room?.version}{current !== student.willEat ? ' · Đã điều chỉnh' : ''}</small>
        </>}</td>}<td>{sourceLabel[student.source] ?? student.source}
        {student.latestReason && <small>{student.latestReason}</small>}</td>
        <td><div className="table-actions"><button type="button" className="button secondary" disabled={!canEdit || decisions.isFetching} onClick={() => open(student, 'edit')}>Ghi ngoại lệ</button>
          <button type="button" className="button secondary" onClick={() => open(student, 'history')}>{decisions.data.isSettled ? 'Lịch sử ngoại lệ trước chốt' : 'Lịch sử'}</button></div></td></tr>})}
      </tbody></table></div>
    </>}
    <Pagination page={page} total={decisions.data?.total ?? 0} pageSize={25} busy={searchWaiting || decisions.isFetching} onChange={setPage} />
    {target && <Modal title={`${target.kind === 'edit' ? 'Ngoại lệ' : 'Lịch sử'}: ${target.student.fullName}`} description={`${target.student.studentCode} · ${target.student.className}`} busy={save.isPending} onClose={() => setTarget(null)}>
      {target.kind === 'edit' && <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!save.isPending && canEdit) save.mutate() }}>
        <p className="form-help">Hiện tại: {sourceLabel[target.student.source]}. {target.student.parentReportedAbsent ? 'Phụ huynh đã đăng ký không ăn / báo vắng.' : ''}</p>
        <label className="field">Thao tác<select value={action} onChange={e => setAction(e.target.value as Action)} disabled={save.isPending || !canEdit}>
          <option value="EAT">Dự kiến có suất</option><option value="ABSENT">Dự kiến không có suất</option><option value="DEFAULT">Khôi phục mặc định</option></select></label>
        <p className="form-help">Khôi phục mặc định bỏ ngoại lệ nhưng vẫn áp dụng đăng ký không ăn / báo vắng của phụ huynh.</p>
        <label className="field">Lý do<textarea required maxLength={500} value={reason} onChange={e => setReason(e.target.value)} disabled={save.isPending || !canEdit} /></label>
        {!canEdit && <p className="error">Đã hết thời gian ghi ngoại lệ. Bản suất giữ nguyên.</p>}
        <div className="form-actions"><button type="button" className="button secondary" disabled={save.isPending} onClick={() => setTarget(null)}>Hủy</button>
          <button type="submit" className="button primary" disabled={save.isPending || !canEdit || !reason.trim()}>Lưu ngoại lệ</button></div>
      </form>}
      <div className="workflow-form"><h3>Lịch sử thao tác</h3>{historyContent}</div>
    </Modal>}
  </section>
}
