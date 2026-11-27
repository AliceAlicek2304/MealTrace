import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { FilterPanel } from '../../components/FilterPanel'
import { Modal } from '../../components/Modal'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { schoolToday } from '../../lib/schoolTime'
import { NutritionValues } from './MenuNutritionView'
import { DataState, DateFields, dateLabel, mealLabel } from './OperationsControls'
import { operationsApi as ops, type DateRange, type PublishedMenu } from './operationsApi'

function firstWeek(): DateRange {
  const from = schoolToday()
  const end = new Date(`${from}T00:00:00Z`)
  end.setUTCDate(end.getUTCDate() + 6)
  return { from, to: end.toISOString().slice(0, 10) }
}

export function ParentMenusPage() {
  const children = useQuery({ queryKey: ['parent-students'], queryFn: ({ signal }) => ops.children(signal) })
  const [childId, setChildId] = useState('')
  const [range, setRange] = useState(firstWeek)
  const [applied, setApplied] = useState(firstWeek)
  const [detail, setDetail] = useState<PublishedMenu | null>(null)
  const [error, setError] = useState('')
  const child = children.data?.find(item => item.studentId === childId) ?? children.data?.[0]
  const menus = useQuery({
    queryKey: ['ops-parent-menus', child?.studentId, applied], enabled: !!child && children.isSuccess,
    queryFn: ({ signal }) => ops.parentMenus(child!.studentId, applied, signal),
  })
  function filter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const days = (Date.parse(range.to) - Date.parse(range.from)) / 86400000 + 1
    if (!(days >= 1 && days <= 62)) { setError('Chọn khoảng ngày từ 1 đến 62 ngày.'); return }
    setError('')
    setApplied(range)
  }
  return <section><h1>Thực đơn của trẻ</h1>
    <div className="panel workflow-lists">
      <div className="panel-head"><h2>Thực đơn đã công bố</h2></div>
      <DataState query={children}>{child ? <>
        <div className="operations-filter"><label className="field"><span>Trẻ</span><select value={child.studentId} onChange={e => setChildId(e.target.value)}>
          {children.data?.map(item => <option key={item.studentId} value={item.studentId}>{item.fullName}{item.className ? ` · ${item.className}` : ''}</option>)}
        </select></label></div>
        <FilterPanel activeCount={1}><form className="operations-filter workflow-form" onSubmit={filter}>
          <DateFields value={range} onChange={value => { setRange(value); setError('') }} required />
          {error && <p role="alert">{error}</p>}
          <button type="submit" className="button secondary operations-inline">Xem thực đơn</button>
        </form></FilterPanel>
        <DataState query={menus}><p className="operations-period">{dateLabel(applied.from)} – {dateLabel(applied.to)}</p>
          <div className="table-wrap"><ResponsiveTable>
            <thead><tr><th>Ngày / bữa</th><th>Món ăn</th><th>Thao tác</th></tr></thead>
            <tbody>{menus.data?.items.map(item => <tr key={item.mealDayId}>
              <td><strong>{mealLabel(item.mealType)}</strong><small>{dateLabel(item.date)}</small></td>
              <td>{item.dishes.map(dish => dish.recipeName).join(', ')}</td>
              <td><button type="button" className="button secondary" onClick={() => setDetail(item)}>Chi tiết</button></td>
            </tr>)}
            {!menus.data?.items.length && <tr><td colSpan={3}>Chưa có thực đơn đã công bố trong khoảng ngày này.</td></tr>}</tbody>
          </ResponsiveTable></div>
        </DataState>
      </> : <p className="empty compact">Chưa có trẻ được liên kết. Vào Liên kết trẻ để gửi yêu cầu.</p>}</DataState>
    </div>
    {detail && <Modal title={`${mealLabel(detail.mealType)} · ${dateLabel(detail.date)}`} onClose={() => setDetail(null)}>
      <div className="workflow-form"><h3>Món ăn</h3><ul className="operations-items">
        {detail.dishes.map(dish => <li key={dish.recipeVersionId}>{dish.recipeName}</li>)}
      </ul><h3>Dinh dưỡng mỗi suất</h3>{detail.nutrition ? <NutritionValues totals={detail.nutrition} /> : <p>Chưa có số liệu dinh dưỡng được công bố.</p>}</div>
    </Modal>}
  </section>
}
