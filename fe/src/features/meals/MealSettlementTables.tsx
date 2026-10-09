import { ResponsiveTable } from '../../components/ResponsiveTable'
import { SectionSwitcher } from '../../components/SectionSwitcher'
import { useState } from 'react'
import { schoolDateTime } from '../../lib/schoolTime'

export type MealSettlement = {
  id: string; classId: string | null; className: string | null; version: number;
  count: number; settledAt: string; settledBy: string; reason: string | null; supersedesId: string | null
}

// Keep the same current-version rule as MealService, including legacy school-wide snapshots.
export function currentSettlements(rows: MealSettlement[]): MealSettlement[] {
  const byClass = new Map<string, MealSettlement>()
  for (const row of rows) {
    if (row.classId === null) continue
    const previous = byClass.get(row.classId)
    if (!previous || row.version > previous.version) byClass.set(row.classId, row)
  }
  if (byClass.size) return [...byClass.values()]
  const legacy = [...rows].sort((a, b) => Date.parse(b.settledAt) - Date.parse(a.settledAt) || a.id.localeCompare(b.id))[0]
  return legacy ? [legacy] : []
}

export function MealSettlementTables({ rows }: { rows: MealSettlement[] }) {
  const [view, setView] = useState<'current' | 'history'>('current')
  const current = currentSettlements(rows)
  const currentIds = new Set(current.map(row => row.id))
  const shown = view === 'current' ? current : [...rows].sort((a, b) => Date.parse(b.settledAt) - Date.parse(a.settledAt) || b.version - a.version || a.id.localeCompare(b.id))
  return <section aria-label="Bản chốt suất">
    <h3>Số suất theo lớp</h3>
    <SectionSwitcher label="Chọn bản chốt" value={view} onChange={setView} options={[{ value: 'current', label: 'Suất hiện hành' }, { value: 'history', label: `Lịch sử bản chốt (${rows.length})` }]} />
    <p className="form-help">{view === 'current' ? 'Mỗi lớp hiển thị bản mới nhất đã chốt hoặc được duyệt.' : 'Bao gồm bản gốc và các bản điều chỉnh. Số suất của các phiên bản không cộng dồn.'}</p>
    <div className="table-wrap"><ResponsiveTable aria-label={view === 'current' ? 'Suất hiện hành' : 'Lịch sử bản chốt'}>
      <thead><tr><th scope="col">Lớp</th><th scope="col">Phiên bản</th><th scope="col">Số suất</th><th scope="col">Trạng thái</th><th scope="col">Thời điểm lưu</th>{view === 'history' && <th scope="col">Lý do</th>}</tr></thead>
      <tbody>{shown.map(row => <tr key={row.id}>
        <td>{row.className ?? 'Bản cũ toàn trường'}</td><td>Bản {row.version}</td><td>{row.count}</td>
        <td>{currentIds.has(row.id) ? 'Hiện hành' : 'Lịch sử'}{row.supersedesId && <small>Điều chỉnh sau chốt</small>}</td>
        <td>{schoolDateTime(row.settledAt)}</td>{view === 'history' && <td>{row.reason ?? (row.supersedesId ? 'Chưa ghi lý do' : 'Bản chốt gốc')}</td>}
      </tr>)}{!shown.length && <tr><td colSpan={view === 'current' ? 5 : 6}>Chưa có bản chốt.</td></tr>}</tbody>
    </ResponsiveTable></div>
  </section>
}
