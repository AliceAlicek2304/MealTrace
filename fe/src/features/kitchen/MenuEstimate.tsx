import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'
import { isAxiosError } from 'axios'
import { toast } from 'sonner'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { schoolDateTime } from '../../lib/schoolTime'
import { DataState, numberLabel, operationToast } from './OperationsControls'
import { kitchenDiff } from './kitchenApi'
import { operationsApi as ops, type Menu } from './operationsApi'

export function MenuEstimate({ menu, busy, onBusy }: Readonly<{ menu: Menu; busy: boolean; onBusy: (busy: boolean) => void }>) {
  const [estimateId, setEstimateId] = useState('')
  const [classId, setClassId] = useState('')
  const [reviewedToken, setReviewedToken] = useState<string | null>(null)
  const [rejectedToken, setRejectedToken] = useState<string | null>(null)
  const { mealDayId, publishedVersion, publishedVersionId } = menu
  const portions = useQuery({ queryKey: ['ops-estimate-portions', mealDayId], queryFn: () => kitchenDiff(mealDayId), refetchOnWindowFocus: false })
  const estimate = useQuery({ queryKey: ['ops-estimate', estimateId], enabled: !!estimateId, queryFn: ({ signal }) => ops.estimate(estimateId, signal) })
  const create = useMutation({
    mutationFn: ops.createEstimate,
    onMutate: () => { onBusy(true) },
    onSuccess: result => { setEstimateId(result.id); setReviewedToken(null); toast.success('Đã tạo dự trù.', { toasterId: 'edit-modal' }) },
    onError: (error, input) => {
      setReviewedToken(null)
      if (isAxiosError(error) && error.response?.data?.code === 'STALE_SETTLEMENT')
        setRejectedToken(input.snapshotTokens[mealDayId])
      operationToast(error)
    },
    onSettled: () => { onBusy(false) },
  })
  const download = useMutation({
    mutationFn: ops.exportEstimate,
    onMutate: () => { onBusy(true) },
    onSuccess: ({ data }) => {
      const url = URL.createObjectURL(data)
      const link = document.createElement('a')
      link.href = url
      link.download = `du-tru-nguyen-lieu-${menu.date}.csv`
      document.body.append(link)
      link.click()
      link.remove()
      window.setTimeout(() => URL.revokeObjectURL(url), 1000)
    },
    onError: operationToast,
    onSettled: () => { onBusy(false) },
  })
  const diff = portions.data
  const chosenClass = diff?.classDiffs.find(room => room.classId === classId)
  const count = chosenClass?.appliedCount ?? diff?.appliedTotal ?? 0
  const canReview = !!(diff && diff.classDiffs.length && (!classId || chosenClass) && rejectedToken !== diff.snapshotToken && !busy && !portions.isFetching && !portions.isError)
  const canCreate = !!(publishedVersionId && diff && canReview && reviewedToken === diff.snapshotToken)
  function createEstimate() {
    if (!canCreate || !diff || !publishedVersionId) return
    create.mutate({
      menuVersionIds: [publishedVersionId], mealDayIds: [mealDayId], classIds: classId ? [classId] : [],
      snapshotTokens: { [mealDayId]: diff.snapshotToken },
    })
  }
  return <div className="workflow-form">
    <div className="entry"><strong>Dự trù cho thực đơn bản {publishedVersion}</strong><p>Số lượng dự kiến dựa trên số suất đã chốt.</p></div>
    <DataState query={portions}>{diff && <>
      <label className="field"><span>Lớp</span><select value={classId} disabled={busy} onChange={e => { setClassId(e.target.value); setReviewedToken(null) }}>
        <option value="">Tất cả lớp</option>
        {diff.classDiffs.map(room => <option key={room.classId} value={room.classId}>{room.className}</option>)}
      </select></label>
      <p><strong>{numberLabel(count)} suất</strong> theo bản chốt hiện hành.</p>
      <label className="switch-line"><input type="checkbox" checked={reviewedToken === diff.snapshotToken} disabled={!canReview} onChange={e => setReviewedToken(e.target.checked ? diff.snapshotToken : null)} /><span>Đã đối chiếu số suất</span></label>
      <div className="form-actions">
        <button type="button" className="button secondary" disabled={busy || portions.isFetching} onClick={() => { setClassId(''); setReviewedToken(null); void portions.refetch() }}>Tải lại suất</button>
        <button type="button" className="button primary" disabled={!canCreate} onClick={createEstimate}>Tạo dự trù</button>
      </div>
    </>}</DataState>
    {estimateId && <DataState query={estimate}>{estimate.data && <>
      <div className="operations-result-head"><div><h3>Kết quả dự trù</h3><small>{schoolDateTime(estimate.data.createdAt)} · {estimate.data.portions.reduce((sum, room) => sum + room.portions, 0)} suất</small></div>
        <button type="button" className="button secondary" disabled={busy} onClick={() => download.mutate(estimateId)}>Tải CSV</button>
      </div>
      <div className="table-wrap"><ResponsiveTable><thead><tr><th>Nguyên liệu</th><th>Lượng cần</th><th>Phần ăn được</th></tr></thead>
        <tbody>{estimate.data.lines.map(line => <tr key={line.ingredientId}>
          <td><strong>{line.ingredientName}</strong></td><td>{numberLabel(line.quantity)} {line.unit}</td><td>{numberLabel(line.edibleGrams)} g</td>
        </tr>)}{!estimate.data.lines.length && <tr><td colSpan={3}>Không có nguyên liệu trong dự trù này.</td></tr>}</tbody>
      </ResponsiveTable></div>
    </>}</DataState>}
  </div>
}
