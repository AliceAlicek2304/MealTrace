import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { getScopeOptions } from './authApi'
import { ClassPicker } from '../../components/ClassPicker'
import { Pagination } from '../../components/Pagination'

export function ScopePicker({ kind, selected, onChange }: { kind: 'classes' | 'students'; selected: string[]; onChange: (ids: string[]) => void }) {
  const [search, setSearch] = useState('')
  const [classId, setClassId] = useState('')
  const [page, setPage] = useState(1)
  const query = useQuery({ queryKey: ['scope-options', kind, search, classId, page, selected.join(',')],
    queryFn: () => getScopeOptions({ search, classId: classId || undefined, classPage: kind === 'classes' ? page : 1,
      studentPage: kind === 'students' ? page : 1, selectedClassIds: kind === 'classes' ? selected.join(',') : undefined,
      selectedStudentIds: kind === 'students' ? selected.join(',') : undefined }) })
  const items = query.data?.[kind] ?? []
  const chosen = (kind === 'classes' ? query.data?.selectedClasses : query.data?.selectedStudents) ?? []
  const toggle = (id: string) => onChange(selected.includes(id) ? selected.filter(x => x !== id) : [...selected, id])
  return <><label className="field">Tìm {kind === 'classes' ? 'lớp / niên khóa' : 'mã hoặc tên trẻ'}<input value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
    {kind === 'students' && <ClassPicker value={classId} onChange={id => { setClassId(id); setPage(1) }} label="Lọc trẻ theo lớp hiện tại" />}
    <p>Đã chọn: {selected.length}</p><div className="inline-choices">{chosen.map(x => <label key={x.id}><input type="checkbox" checked onChange={() => toggle(x.id)} /> {x.name}</label>)}</div>
    {query.isPending ? <p>Đang tải…</p> : query.isError ? <p className="error">Không tải được phạm vi. Hãy thử lại.</p> : <div className="inline-choices">{items.filter(x => !selected.includes(x.id)).map(x => <label key={x.id}><input type="checkbox" checked={false} onChange={() => toggle(x.id)} /> {x.name}</label>)}{!items.length && <p>Không có kết quả.</p>}</div>}
    <Pagination page={page} total={(kind === 'classes' ? query.data?.classTotal : query.data?.studentTotal) ?? 0} pageSize={25} busy={query.isFetching} onChange={setPage} />
  </>
}
