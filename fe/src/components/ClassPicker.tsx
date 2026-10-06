import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getScopeOptions } from '../features/access/authApi'
import { Pagination } from './Pagination'

export function ClassPicker({ value, onChange, label = 'Lớp', required = false, disabled = false, compact = false }: {
  value: string; onChange: (id: string) => void; label?: string; required?: boolean; disabled?: boolean; compact?: boolean
}) {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const query = useQuery({ queryKey: ['scope-options', 'classes', search, page, value],
    queryFn: () => getScopeOptions({ search, classPage: page, selectedClassIds: value }) })
  const options = [...new Map([...(query.data?.selectedClasses ?? []), ...(query.data?.classes ?? [])].map(x => [x.id, x])).values()]
  const searchControls = <><label className="field">Tìm lớp<input disabled={disabled} value={search} placeholder="Tên lớp hoặc niên khóa" onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
    {(page > 1 || (query.data?.classTotal ?? 0) > 25) && <Pagination page={page} total={query.data?.classTotal ?? 0} pageSize={25} busy={query.isFetching || disabled} onChange={setPage} />}</>
  return <div className={`class-picker ${compact ? 'class-picker-compact' : ''}`}>
    {!compact && searchControls}
    <label className="field">{label}<select required={required} disabled={disabled || query.isPending || query.isError} value={value} onChange={e => onChange(e.target.value)}>
      <option value="">{required ? 'Chọn lớp' : 'Tất cả lớp'}</option>{options.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
    {query.isError && <p className="error">Không tải được lớp.</p>}
    {compact && <details className="picker-search"><summary>Tìm lớp khác</summary>{searchControls}</details>}
  </div>
}
