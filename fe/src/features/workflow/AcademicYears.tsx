import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'

type Year = { code: string; startDate: string | null; endDate: string | null }
type Preview = { code: string; startDate: string; endDate: string; sourceYearCode: string; isConfigured: boolean }

export function AcademicYears() {
  const cache = useQueryClient()
  const years = useQuery({ queryKey: ['admin-academic-years'], queryFn: async () => (await api.get<Year[]>('/admin/academic-years')).data })
  const [target, setTarget] = useState<Year | null>(null)
  const [code, setCode] = useState('')
  const [sourceCode, setSourceCode] = useState<string | null>(null)
  const [start, setStart] = useState('')
  const [end, setEnd] = useState('')
  const preview = useMutation({ mutationFn: async (yearCode: string) => (await api.get<Preview>(`/admin/academic-years/${encodeURIComponent(yearCode)}/next`)).data,
    onSuccess: data => {
      if (data.isConfigured) { toast.error('Năm học tiếp theo đã được thiết lập.'); return }
      setTarget({ code: data.code, startDate: null, endDate: null }); setCode(data.code); setSourceCode(data.sourceYearCode); setStart(data.startDate); setEnd(data.endDate)
    }, onError: error => toast.error(apiErrorMessage(error)) })
  function open(year: Year) { setTarget(year); setCode(year.code); setSourceCode(null); setStart(''); setEnd('') }
  const save = useMutation({ mutationFn: () => api.put(`/admin/academic-years/${encodeURIComponent(code)}`, { startDate: start, endDate: end, sourceYearCode: sourceCode }),
    onSuccess: async () => { setTarget(null); toast.success('Đã thiết lập mốc năm học.'); await Promise.all([
      cache.invalidateQueries({ queryKey: ['admin-academic-years'] }), cache.invalidateQueries({ queryKey: ['academic-years'] }), cache.invalidateQueries({ queryKey: ['parent-students'] })]) },
    onError: error => toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }) })
  return <section className="panel workflow-lists"><div className="panel-head"><div><h2>Năm học dùng chung</h2><p>Nhập lịch trường một lần cho năm đầu. Các lớp dùng chung; năm sau sao chép lịch cũ, kiểm tra và lưu. Mốc đã lưu cần đối chiếu dữ liệu trước khi thay đổi.</p></div>
    <button type="button" className="button primary" disabled={preview.isPending || years.isPending || years.isError} onClick={() => open({ code: '', startDate: null, endDate: null })}>Tạo năm học</button></div>
    {years.isPending ? <p className="empty compact">Đang tải…</p> : years.isError ? <p className="empty compact error">{apiErrorMessage(years.error)}</p> : years.data?.map(year => <div className="entry" key={year.code}><div><strong>{year.code}</strong><small>{year.startDate ? `${year.startDate} đến ${year.endDate}` : 'Chưa thiết lập'}</small></div>
      {!year.startDate ? <button type="button" className="button secondary" disabled={preview.isPending} onClick={() => open(year)}>Thiết lập ngày</button> :
        <button type="button" className="button secondary" disabled={preview.isPending} onClick={() => preview.mutate(year.code)}>Tạo năm học tiếp theo</button>}</div>)}
    {target && <Modal title={sourceCode ? `Tạo năm học ${code}` : target.code ? `Năm học ${target.code}` : 'Tạo năm học'} description={sourceCode ? `Lịch đề xuất sao chép từ ${sourceCode} và tăng một năm. Kiểm tra ngày thực tế trước khi lưu.` : 'Nhập ngày bắt đầu và kết thúc thực tế của trường. Chỉ cần nhập một lần cho toàn trường.'} busy={save.isPending} onClose={() => setTarget(null)}>
      <form className="workflow-form" onSubmit={e => { e.preventDefault(); if (!save.isPending) save.mutate() }}>
        <label className="field">Niên khóa<input required pattern="[0-9]{4}-[0-9]{4}" placeholder="2026-2027" value={code} disabled={save.isPending} readOnly={!!target.code} onChange={e => setCode(e.target.value)} /></label>
        <label className="field">Ngày bắt đầu<input type="date" required disabled={save.isPending} value={start} onChange={e => setStart(e.target.value)} /></label>
        <label className="field">Ngày kết thúc<input type="date" required min={start} disabled={save.isPending} value={end} onChange={e => setEnd(e.target.value)} /></label>
        {sourceCode && <label className="form-help"><input type="checkbox" required disabled={save.isPending} /> Tôi đã kiểm tra ngày bắt đầu và kết thúc theo lịch trường.</label>}
        <div className="form-actions"><button type="button" className="button secondary" disabled={save.isPending} onClick={() => setTarget(null)}>Hủy</button><button type="submit" className="button primary" disabled={save.isPending}>Lưu mốc năm học</button></div>
      </form></Modal>}
  </section>
}
