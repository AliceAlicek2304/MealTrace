import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, apiErrorMessage } from '../../lib/api'
import { toast } from 'sonner'
import { MealExceptions } from './MealExceptions'

type Day = { id: string; date: string; mealType: string; schoolYear: string | null; cutoffAt: string; isSettled: boolean }
type Room = { classId: string; className: string; schoolYear: string; studentIds: string[]; studentNames: string[]; absentStudentIds: string[]; isSettled: boolean }
type Portions = { id: string; date: string; mealType: string; cutoffAt: string; classes: Room[] }

export function PortionsPage({ roles }: { roles: string[] }) {
  const queryClient = useQueryClient()
  const isAdmin = roles.includes('ADMIN')
  const isTeacher = roles.includes('TEACHER')
  const [selected, setSelected] = useState('')
  const [date, setDate] = useState('')
  const [mealType, setMealType] = useState('Bữa trưa')
  const [schoolYear, setSchoolYear] = useState('2026-2027')
  const days = useQuery({ queryKey: ['workflow-days'], queryFn: async () => (await api.get<Day[]>('/meal-days/workflow')).data })
  const portions = useQuery({ queryKey: ['portions', selected], enabled: !!selected,
    queryFn: async () => (await api.get<Portions>(`/meal-days/${selected}/portions`)).data })
  const createDay = useMutation({ mutationFn: () => api.post<Day>('/meal-days', { date, mealType, schoolYear }),
    onSuccess: async response => { setSelected(response.data.id); toast.success('Đã tạo phiên ăn.'); await queryClient.invalidateQueries({ queryKey: ['workflow-days'] }) },
    onError: error => toast.error(apiErrorMessage(error)) })
  const settle = useMutation({ mutationFn: () => api.post(`/meal-days/${selected}/settle`),
    onSuccess: async () => { toast.success('Đã chốt số suất và lưu danh sách trẻ nguồn cho bếp.'); await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['workflow-days'] }), queryClient.invalidateQueries({ queryKey: ['portions', selected] }), queryClient.invalidateQueries({ queryKey: ['meal-decisions', selected] })]) },
    onError: error => toast.error(apiErrorMessage(error)) })
  function submitDay(event: FormEvent) { event.preventDefault(); createDay.mutate() }
  const picked = days.data?.find(day => day.id === selected)
  const total = portions.data?.classes.reduce((sum, room) => sum + room.studentIds.length, 0) ?? 0

  return <><div className="eyebrow">SỐ SUẤT GỬI BẾP</div><h1>Danh sách dự kiến ăn</h1>
    <p className="lead">Số suất lấy từ ghi danh tại ngày ăn, báo vắng đúng hạn và các ngoại lệ đã ghi. Bản chốt giữ nguyên sau giờ chốt.</p>
    {isAdmin && <section className="panel"><h2>Tạo phiên ăn</h2><form className="workflow-form workflow-fields" onSubmit={submitDay}>
      <label className="field">Ngày ăn<input type="date" required value={date} onChange={e => setDate(e.target.value)} /></label>
      <label className="field">Phiên ăn<input required maxLength={60} value={mealType} onChange={e => setMealType(e.target.value)} /></label>
      <label className="field">Niên khóa<input required pattern="[0-9]{4}-[0-9]{4}" value={schoolYear} onChange={e => setSchoolYear(e.target.value)} /></label>
      <button type="submit" className="button primary" disabled={createDay.isPending}>Tạo phiên</button>
    </form></section>}
    <section className="panel workflow-lists"><h2>Chọn phiên ăn</h2><div className="workflow-form">
      {days.isError ? <p className="error">Không tải được phiên ăn.</p> : <select value={selected} onChange={e => setSelected(e.target.value)}><option value="">Chọn ngày và phiên ăn</option>
        {days.data?.map(day => <option key={day.id} value={day.id}>{day.date} · {day.mealType} · {day.schoolYear ?? 'Chưa gắn niên khóa'} {day.isSettled ? '· Đã chốt' : ''}</option>)}</select>}
    </div></section>
    {selected && <section className="panel workflow-lists"><div className="panel-head"><div><h2>{picked?.mealType ?? 'Phiên ăn'} · {picked?.date}</h2>
      <p>Giờ chốt: {picked ? new Date(picked.cutoffAt).toLocaleString('vi-VN') : '—'} · {total} suất</p></div>
      {isAdmin && picked && !picked.isSettled && <button type="button" className="button primary" disabled={settle.isPending || new Date() < new Date(picked.cutoffAt)} onClick={() => settle.mutate()}>Chốt và gửi bếp</button>}</div>
      {portions.isPending ? <p className="empty compact">Đang tính số suất…</p> : portions.isError ? <p className="empty compact error">Không tải được danh sách.</p> : portions.data?.classes.map(room =>
        <div className="entry" key={room.classId}><strong>{room.className} · {room.studentIds.length} suất</strong><small>{room.isSettled ? 'Bản đã chốt' : `${room.absentStudentIds.length} trẻ không có suất dự kiến`}</small>
          <div className="workflow-names">{room.studentNames.join(', ') || 'Không có trẻ dự kiến ăn'}</div>
        </div>)}</section>}
    {selected && (isAdmin || isTeacher) && <MealExceptions key={selected} mealId={selected} />}
  </>
}
