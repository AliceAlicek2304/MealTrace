import { FilterPanel } from '../../components/FilterPanel'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { SectionSwitcher } from '../../components/SectionSwitcher'
import { useDeadline } from '../../lib/useDeadline'
import { schoolDateTime, schoolTime } from '../../lib/schoolTime'
import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { toast } from 'sonner'
import { MealExceptions } from './MealExceptions'
import { PortionAmendments } from './PortionAmendments'
import { SchoolYearPicker } from '../../components/SchoolYearPicker'
import { Pagination } from '../../components/Pagination'
import { Modal } from '../../components/Modal'

type Day = { id: string; date: string; mealType: string; schoolYear: string | null; cutoffAt: string; isSettled: boolean; isCancelled: boolean; cancellationReason: string | null }
type DayPage = { items: Day[]; total: number }
type Room = { classId: string; className: string; schoolYear: string; studentIds: string[]; studentNames: string[]; absentStudentIds: string[]; isSettled: boolean; version: number; originalCount: number | null; count: number | null; kitchenAdjustment: number }
type Portions = { id: string; date: string; mealType: string; cutoffAt: string; classes: Room[]; isCancelled: boolean; cancellationReason: string | null; isSettled: boolean }

export function PortionsPage({ roles }: { roles: string[] }) {
  const queryClient = useQueryClient()
  const isAdmin = roles.includes('ADMIN')
  const isTeacher = roles.includes('TEACHER')
  const [selected, setSelected] = useState('')
  const [picked, setPicked] = useState<Day | null>(null)
  const [dayPage, setDayPage] = useState(1)
  const [filterDate, setFilterDate] = useState('')
  const [date, setDate] = useState('')
  const [mealType, setMealType] = useState('Bữa trưa')
  const [schoolYear, setSchoolYear] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const [detailTab, setDetailTab] = useState<'portions' | 'exceptions' | 'amendments'>('portions')
  const [roster, setRoster] = useState<Room | null>(null)
  const [confirmSettle, setConfirmSettle] = useState(false)
  const days = useQuery({ queryKey: ['workflow-days', filterDate, dayPage], queryFn: async () => (await api.get<DayPage>('/meal-days/workflow', { params: { date: filterDate || undefined, page: dayPage } })).data })
  const portions = useQuery({ queryKey: ['portions', selected], enabled: !!selected,
    queryFn: async () => (await api.get<Portions>(`/meal-days/${selected}/portions`)).data })
  const createDay = useMutation({ mutationFn: () => api.post<Day>('/meal-days', { date, mealType, schoolYear }),
    onSuccess: async response => { setCreateOpen(false); setDetailTab('portions'); setSelected(response.data.id); setPicked(response.data); toast.success('Đã tạo phiên ăn.'); await queryClient.invalidateQueries({ queryKey: ['workflow-days'] }) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  const settle = useMutation({ mutationFn: () => api.post(`/meal-days/${selected}/settle`),
    onSuccess: async () => { setConfirmSettle(false); toast.success('Đã chốt số suất và lưu danh sách trẻ nguồn cho bếp.', { toasterId: 'edit-modal' }); await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['workflow-days'] }), queryClient.invalidateQueries({ queryKey: ['portions', selected] }), queryClient.invalidateQueries({ queryKey: ['meal-decisions', selected] })]) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  function submitDay(event: FormEvent) { event.preventDefault(); createDay.mutate() }
  const current = days.data?.items.find(day => day.id === selected) ?? picked
  const cutoffPassed = useDeadline(portions.data?.cutoffAt ?? current?.cutoffAt)
  const cancelled = portions.data?.isCancelled ?? current?.isCancelled
  const settled = portions.data?.isSettled ?? current?.isSettled
  const total = portions.data?.classes.reduce((sum, room) => sum + (room.count ?? room.studentIds.length), 0) ?? 0

  return <><div className="eyebrow">SỐ SUẤT GỬI BẾP</div><h1>Danh sách dự kiến ăn</h1>
    <p className="lead">Số suất lấy từ ghi danh tại ngày ăn, báo vắng đúng hạn và các ngoại lệ đã ghi. Bản chốt giữ nguyên sau giờ chốt.</p>
    {createOpen && <Modal title="Tạo phiên ăn" busy={createDay.isPending} onClose={() => setCreateOpen(false)}><form className="workflow-form" onSubmit={submitDay}>
      <label className="field">Ngày ăn<input type="date" required value={date} onChange={e => setDate(e.target.value)} /></label>
      <label className="field">Phiên ăn<input required maxLength={60} value={mealType} onChange={e => setMealType(e.target.value)} /></label>
      <SchoolYearPicker value={schoolYear} onChange={setSchoolYear} configuredOnly />
      <button type="submit" className="button primary" disabled={createDay.isPending}>Tạo phiên</button>
    </form></Modal>}
    <section className="panel workflow-lists"><div className="panel-head"><h2>Danh sách phiên ăn</h2>{isAdmin && <button type="button" className="button primary" onClick={() => setCreateOpen(true)}>Tạo phiên ăn</button>}</div><FilterPanel activeCount={filterDate ? 1 : 0}><div className="list-toolbar">
      <label className="field">Lọc ngày ăn<input type="date" value={filterDate} onChange={e => { setFilterDate(e.target.value); setDayPage(1) }} /></label>
      <button type="button" className="button secondary" onClick={() => { setFilterDate(''); setDayPage(1) }}>Bỏ lọc</button></div></FilterPanel>
      {days.isPending ? <p className="empty compact">Đang tải phiên ăn…</p> : days.isError ? <p className="empty compact error">Không tải được phiên ăn.</p> : <div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Ngày ăn</th><th scope="col">Bữa ăn</th><th scope="col">Năm học</th><th scope="col">Giờ chốt</th><th scope="col">Trạng thái</th><th scope="col">Thao tác</th></tr></thead><tbody>
        {days.data?.items.map(day => <tr key={day.id}><td>{day.date}</td><td><strong>{day.mealType}</strong></td><td>{day.schoolYear ?? 'Chưa gắn năm học'}</td><td>{schoolTime(day.cutoffAt)}</td><td><span className={`status ${day.isCancelled ? 'suspended' : day.isSettled ? 'active' : ''}`}>{day.isCancelled ? 'Đã hủy' : day.isSettled ? 'Đã chốt' : 'Chưa chốt'}</span></td><td><button type="button" className="button secondary" onClick={() => { setSelected(day.id); setPicked(day); setDetailTab('portions') }}>Xem / Quản lý</button></td></tr>)}
        {!days.data?.items.length && <tr><td colSpan={6} className="empty compact">Không có phiên ăn phù hợp.</td></tr>}
      </tbody></ResponsiveTable></div>}
      <Pagination page={dayPage} total={days.data?.total ?? 0} pageSize={25} busy={days.isFetching} onChange={setDayPage} />
    </section>
    {selected && <Modal wide title={`${current?.mealType ?? 'Phiên ăn'} · ${current?.date ?? ''}`} busy={settle.isPending} onClose={() => { setSelected(''); setPicked(null); setRoster(null); setConfirmSettle(false) }}>
      <SectionSwitcher label="Nội dung phiên ăn" value={detailTab} onChange={setDetailTab} options={[
        { value: 'portions', label: 'Suất theo lớp' },
        ...(isAdmin || isTeacher ? [{ value: 'exceptions' as const, label: settled ? 'Nguồn trước điều chỉnh' : 'Nguồn / Ngoại lệ' }] : []),
        ...(settled && !cancelled ? [{ value: 'amendments' as const, label: 'Điều chỉnh sau chốt' }] : []),
      ]} />
    {detailTab === 'portions' && <section className="workflow-lists"><div className="panel-head"><div><h2>Suất ăn theo lớp</h2>
      <p>Giờ chốt: {current ? schoolDateTime(current.cutoffAt) : '—'} · {total} suất</p>{cancelled && <p className="error">Đã hủy: {portions.data?.cancellationReason ?? current?.cancellationReason}</p>}</div>
      {isAdmin && current && !settled && !cancelled && <button type="button" className="button primary" disabled={settle.isPending || !cutoffPassed} onClick={() => setConfirmSettle(true)}>Chốt và gửi bếp</button>}</div>
      {portions.isPending ? <p className="empty compact">Đang tính số suất…</p> : portions.isError ? <p className="empty compact error">Không tải được danh sách.</p> : <div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">Lớp</th><th scope="col">Năm học</th><th scope="col">Số suất</th><th scope="col">Trạng thái / phiên bản</th><th scope="col">Thao tác</th></tr></thead><tbody>{portions.data?.classes.map(room =>
        <tr key={room.classId}><td><strong>{room.className}</strong></td><td>{room.schoolYear}</td><td>{room.count ?? room.studentIds.length}{room.kitchenAdjustment !== 0 && <small>{room.studentIds.length} trẻ · bếp {room.kitchenAdjustment > 0 ? "+" : ""}{room.kitchenAdjustment}</small>}</td><td>{room.isSettled ? `Bản ${room.version} · gốc ${room.originalCount} suất` : `${room.absentStudentIds.length} trẻ không có suất dự kiến`}</td>
          <td><button type="button" className="button secondary" onClick={() => setRoster(room)}>Danh sách trẻ</button></td></tr>)}
        {!portions.data?.classes.length && <tr><td colSpan={5} className="empty compact">Chưa có suất theo lớp.</td></tr>}</tbody></ResponsiveTable></div>}</section>}
    {detailTab === 'exceptions' && (isAdmin || isTeacher) && <MealExceptions key={selected} mealId={selected} />}
    {detailTab === 'amendments' && settled && !cancelled && portions.data && <PortionAmendments key={selected} mealId={selected} rooms={portions.data.classes} roles={roles} />}
    </Modal>}
    {roster && <Modal title={`Trẻ có suất · ${roster.className}`} onClose={() => setRoster(null)}><p className="workflow-form">{roster.studentIds.length} trẻ có suất · gửi bếp {roster.count ?? roster.studentIds.length} suất · điều chỉnh số lượng bếp {roster.kitchenAdjustment > 0 ? "+" : ""}{roster.kitchenAdjustment}</p><div className="table-wrap"><ResponsiveTable><thead><tr><th scope="col">STT</th><th scope="col">Họ tên trẻ</th></tr></thead><tbody>{roster.studentNames.map((name, index) => <tr key={roster.studentIds[index]}><td>{index + 1}</td><td>{name}</td></tr>)}{!roster.studentNames.length && <tr><td colSpan={2} className="empty compact">Không có trẻ có suất.</td></tr>}</tbody></ResponsiveTable></div></Modal>}
    {confirmSettle && <Modal title="Xác nhận chốt suất" busy={settle.isPending} onClose={() => setConfirmSettle(false)}><div className="workflow-form"><p>Chốt {total} suất của {current?.mealType} ngày {current?.date} và gửi bếp? Bản chốt giữ nguyên; thay đổi sau đó cần gửi yêu cầu điều chỉnh.</p><div className="form-actions"><button type="button" className="button secondary" disabled={settle.isPending} onClick={() => setConfirmSettle(false)}>Quay lại</button><button type="button" className="button primary" disabled={settle.isPending || !cutoffPassed || cancelled || settled || portions.isPending || portions.isError} onClick={() => settle.mutate()}>Xác nhận chốt</button></div></div></Modal>}
  </>
}
