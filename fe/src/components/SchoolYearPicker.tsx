import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'

type Year = { code: string; startDate: string | null; endDate: string | null }

export function SchoolYearPicker({ value, onChange, configuredOnly = false }: Readonly<{ value: string; onChange: (code: string) => void; configuredOnly?: boolean }>) {
  const years = useQuery({ queryKey: ['admin-academic-years'], queryFn: async () => (await api.get<Year[]>('/admin/academic-years')).data })
  const options = years.data?.filter(year => !configuredOnly || year.startDate) ?? []
  return <label className="field">Niên khóa<select required disabled={years.isPending || years.isError} value={value} onChange={e => onChange(e.target.value)}>
    <option value="">Chọn năm học</option>{options.map(year => <option key={year.code} value={year.code}>{year.code}{!year.startDate ? ' · Chưa thiết lập ngày' : ''}</option>)}</select>
    {years.isError ? <small className="error">Không tải được năm học.</small> : !years.isPending && !options.length && <small>Tạo năm học tại Lớp và trẻ trước.</small>}
  </label>
}
