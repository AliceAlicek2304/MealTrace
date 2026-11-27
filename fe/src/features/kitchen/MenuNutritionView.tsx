import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { DataState, fieldText, numberLabel } from './OperationsControls'
import { operationsApi as ops, type Totals } from './operationsApi'

const nutrients = [
  { key: 'kcal', label: 'Năng lượng', unit: 'kcal' },
  { key: 'proteinG', label: 'Đạm', unit: 'g' },
  { key: 'fatG', label: 'Chất béo', unit: 'g' },
  { key: 'carbG', label: 'Carbohydrate', unit: 'g' },
] as const
const nutrientLabels = new Map<string, string>(nutrients.map(({ key, label }) => [key, label]))
const statuses: Record<string, string> = { below: 'Dưới khoảng', within: 'Trong khoảng', above: 'Vượt khoảng', unknown: 'Chưa đủ dữ liệu' }
const comparisonNotes: Record<string, string> = { reference_missing: 'Chưa có khoảng tham chiếu cho nhóm tuổi này.', reference_basis_mismatch: 'Khoảng tham chiếu chưa phù hợp với bữa ăn.' }

export function NutritionValues({ totals }: Readonly<{ totals: Totals }>) {
  return <dl className="operations-nutrients">{nutrients.map(({ key, label, unit }) => <div key={key}><dt>{label}</dt>
    <dd>{numberLabel(totals[key])}{totals[key] === null ? '' : ` ${unit}`}</dd>
  </div>)}</dl>
}

export function MenuNutritionView({ id, publishedVersion }: Readonly<{ id: string; publishedVersion: number }>) {
  const [version, setVersion] = useState(publishedVersion)
  const [recompute, setRecompute] = useState<{ ageGroup?: string; validAt?: string; knownAt?: string } | null>(null)
  const history = useQuery({ queryKey: ['ops-menu-history', id], queryFn: ({ signal }) => ops.menuHistory(id, signal) })
  const params = { version, mode: recompute ? 'recompute' as const : 'pinned' as const, ...recompute }
  const query = useQuery({ queryKey: ['ops-menu-nutrition', id, params], queryFn: ({ signal }) => ops.nutrition(id, params, signal) })
  function calculate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const data = new FormData(event.currentTarget)
    const validAt = fieldText(data, 'validAt')
    const knownAt = fieldText(data, 'knownAt')
    setRecompute({ ageGroup: fieldText(data, 'ageGroup') || undefined, validAt: validAt ? `${validAt}:00+07:00` : undefined, knownAt: knownAt ? `${knownAt}:00+07:00` : undefined })
  }
  return <div className="workflow-form">
    <DataState query={history}><label className="field"><span>Phiên bản đã công bố</span><select value={version} onChange={e => { setVersion(Number(e.target.value)); setRecompute(null) }}>
      {history.data?.items.filter(item => item.state === 'published').map(item => <option key={item.versionId} value={item.version}>Bản {item.version}</option>)}
    </select></label></DataState>
    <details className="entry"><summary>Tính lại dinh dưỡng</summary>
      <form className="workflow-form" onSubmit={calculate}>
        <label className="field"><span>Nhóm tuổi (tùy chọn)</span><input name="ageGroup" maxLength={50} /></label>
        <div className="form-grid">
          <label className="field"><span>Thời điểm áp dụng (giờ Việt Nam)</span><input type="datetime-local" name="validAt" /></label>
          <label className="field"><span>Dữ liệu ghi nhận đến (giờ Việt Nam)</span><input type="datetime-local" name="knownAt" /></label>
        </div>
        <div className="form-actions"><button type="button" className="button secondary" disabled={!recompute} onClick={() => setRecompute(null)}>Xem bản công bố</button><button type="submit" className="button primary" disabled={query.isFetching}>Tính lại</button></div>
      </form>
    </details>
    <DataState query={query}>{query.data && <>
      <h3>{recompute ? 'Kết quả tính lại' : 'Dinh dưỡng đã công bố'} mỗi suất</h3>
      <NutritionValues totals={query.data.payload.total} />
      {query.data.asPublished && <details className="entry"><summary>Số liệu đã công bố và chênh lệch</summary>
        <p>Đã công bố</p><NutritionValues totals={query.data.asPublished.total} />
        {query.data.deltaFromPublished && <><p>Chênh lệch so với bản công bố</p><NutritionValues totals={query.data.deltaFromPublished} /></>}
      </details>}
      {query.data.payload.ageGroup && <p>Nhóm tuổi: {query.data.payload.ageGroup}</p>}
      {comparisonNotes[query.data.payload.ageComparisonStatus] && <p>{comparisonNotes[query.data.payload.ageComparisonStatus]}</p>}
      {query.data.payload.ageComparison && <div className="table-wrap"><ResponsiveTable><thead><tr><th>Chỉ tiêu</th><th>Giá trị</th><th>Khoảng tham chiếu</th><th>Đánh giá</th></tr></thead><tbody>
        {query.data.payload.ageComparison.items.map(item => <tr key={item.nutrient}>
          <td>{nutrientLabels.get(item.nutrient) ?? item.nutrient}</td><td>{numberLabel(item.value)}</td>
          <td>{numberLabel(item.min)} – {numberLabel(item.max)}<small>{item.source} · Bản {item.version}</small></td><td>{statuses[item.status] ?? item.status}</td>
        </tr>)}
      </tbody></ResponsiveTable></div>}
      <details className="entry"><summary>Dinh dưỡng từng món</summary>
        {query.data.payload.dishes.map(dish => <div className="operations-nutrition-dish" key={dish.recipeVersionId}><strong>{dish.recipeName}</strong><NutritionValues totals={dish.perServing} /></div>)}
      </details>
    </>}</DataState>
  </div>
}
