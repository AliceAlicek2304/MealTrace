import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { useDebouncedValue } from '../../lib/useDebouncedValue'

export type Child = { studentId: string; studentName: string }
export type Candidate = Child & { studentCode: string; willEat: boolean; source: string }
export type Snapshot = { id: string; version: number; count: number; kitchenAdjustment: number; students: Child[]; decisions: Candidate[] }
export type Request = Child & { id: string; studentCode: string; baseSettlementId: string; baseVersion: number; wasEating: boolean; willEat: boolean;
  quantity: number; isQuantityOnly: boolean; students: (Child & { studentCode: string })[]; reason: string; requestedByName: string; requestedAt: string; status: 'PENDING' | 'APPROVED' | 'REJECTED';
  reviewReason: string | null; reviewedByName: string | null; reviewedAt: string | null }
export type Result = { className: string; canRequest: boolean; original: Snapshot; current: Snapshot; hasOriginalSources: boolean; hasCompleteRoster: boolean;
  added: Child[]; removed: Child[]; candidates: Candidate[]; candidateTotal: number; items: Request[]; total: number }
export type Room = { classId: string; className: string }
export const statuses = { PENDING: 'Chờ xử lý', APPROVED: 'Đã duyệt', REJECTED: 'Đã từ chối' }
export const sources: Record<string, string> = { DEFAULT: 'Mặc định', PARENT_ABSENCE: 'Phụ huynh báo không ăn', STAFF_EAT: 'Ngoại lệ có suất',
  STAFF_ABSENT: 'Ngoại lệ không có suất', APPROVED_AMENDMENT: 'Điều chỉnh đã duyệt', LEGACY_OR_CURRENT_ENROLLMENT: 'Nguồn cũ hoặc ghi danh theo ngày' }
export type PortionAmendmentProps = Readonly<{ mealId: string; rooms: Room[]; roles: string[] }>

export function usePortionAmendments({ mealId, rooms, roles }: PortionAmendmentProps) {
  const cache = useQueryClient()
  const isAdmin = roles.includes('ADMIN')
  const [pickedClass, setPickedClass] = useState('')
  const classId = rooms.some(x => x.classId === pickedClass) ? pickedClass : rooms[0]?.classId ?? ''
  const [search, setSearch] = useState('')
  const searchTerm = useDebouncedValue(search.trim())
  const searchWaiting = search.trim() !== searchTerm
  const [candidatePage, setCandidatePage] = useState(1)
  const [page, setPage] = useState(1)
  const [view, setView] = useState<'requests' | 'candidates'>('requests')
  const [snapshotDialog, setSnapshotDialog] = useState<{ title: string; snapshot: Snapshot } | null>(null)
  const [target, setTarget] = useState<{ kind: 'request'; children: Candidate[]; quantity: number; willEat: boolean; baseId: string; baseVersion: number; classId: string; beforeCount: number } | { kind: 'review'; request: Request } | null>(null)
  const [selected, setSelected] = useState<Candidate[]>([])
  const [selectedBase, setSelectedBase] = useState('')
  const [willEat, setWillEat] = useState(false)
  const [quantityOnly, setQuantityOnly] = useState(false)
  const [quantity, setQuantity] = useState(1)
  const [reason, setReason] = useState('')
  const endpoint = `/meal-days/${mealId}/amendments`
  const query = useQuery({ queryKey: ['portion-amendments', mealId, classId, searchTerm, page, candidatePage], enabled: !searchWaiting && !!classId,
    queryFn: async () => (await api.get<Result>(endpoint, { params: { classId, q: searchTerm, page, candidatePage } })).data, refetchInterval: 15000 })
  const detailId = target?.kind === 'review' ? target.request.id : ''
  const detail = useQuery({ queryKey: ['portion-amendment-detail', mealId, detailId], enabled: !!detailId,
    queryFn: async () => (await api.get<{ before: Snapshot; after: Snapshot | null }>(`${endpoint}/${detailId}`)).data })
  async function refresh() {
    await Promise.all([cache.invalidateQueries({ queryKey: ['portion-amendments', mealId] }),
      cache.invalidateQueries({ queryKey: ['portions', mealId] }), cache.invalidateQueries({ queryKey: ['meal-days'] }),
      cache.invalidateQueries({ queryKey: ['workflow-days'] }), cache.invalidateQueries({ queryKey: ['meal-day', mealId] }), cache.invalidateQueries({ queryKey: ['portion-amendment-detail', mealId] })])
  }
  const request = useMutation({ mutationFn: () => {
    if (target?.kind !== 'request' || target.baseId !== data?.current.id) throw new Error('Chưa chọn phiếu.')
    return api.post(endpoint, { classId: target.classId, studentIds: target.children.map(x => x.studentId), quantity: target.quantity, baseSettlementId: target.baseId, willEat: target.willEat, reason })
  }, onSuccess: async () => { setTarget(null); setSelected([]); setSelectedBase(''); toast.success('Đã gửi yêu cầu. Số suất chỉ thay đổi khi Admin duyệt.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const review = useMutation({ mutationFn: (approve: boolean) => {
    if (target?.kind !== 'review') throw new Error('Chưa chọn yêu cầu.')
    return api.post(`${endpoint}/${target.request.id}/review`, { approve, reason })
  }, onSuccess: async (_, approve) => { setTarget(null); toast.success(approve ? 'Đã duyệt và lưu bản suất mới cho bếp.' : 'Đã từ chối yêu cầu.'); await refresh() },
    onError: async error => { toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' }); await refresh() } })
  const busy = request.isPending || review.isPending
  const data = query.data
  const selectionStale = !!selected.length && selectedBase !== data?.current.id
  const eligibleInPage = data?.candidates.filter(child => child.willEat !== willEat) ?? []
  const selectedIds = new Set(selected.map(child => child.studentId))
  const unselectedInPage = eligibleInPage.filter(child => !selectedIds.has(child.studentId))
  const delta = willEat ? 1 : -1
  const selectedQuantity = quantityOnly ? quantity : selected.length
  const previewCount = data ? data.current.count + delta * selectedQuantity : 0
  const shownRequest = target?.kind === 'review' ? data?.items.find(x => x.id === target.request.id) ?? target.request : null
  const stale = target?.kind === 'request' ? target.baseId !== data?.current.id : shownRequest?.baseSettlementId !== data?.current.id

  function createRequest() {
    if (!data) return
    setReason('')
    setTarget({ kind: 'request', children: quantityOnly ? [] : [...selected], quantity: selectedQuantity, willEat,
      baseId: data.current.id, baseVersion: data.current.version, classId, beforeCount: data.current.count })
  }
  const selectionInvalid = invalidSelection(quantityOnly, selectedQuantity, selectedBase, data?.current.id)
  const exceedsCurrentCount = !willEat && selectedQuantity > (data?.current.count ?? 0)
  return { rooms, isAdmin, classId, query, detail, data, search, searchWaiting, candidatePage, page, view, snapshotDialog, target,
    selected, selectedBase, willEat, quantityOnly, quantity, reason, busy, selectionStale, unselectedInPage, previewCount,
    shownRequest, stale, request, review, selectedQuantity, selectionInvalid, exceedsCurrentCount, createRequest,
    setPickedClass, setSelected, setSelectedBase, setSearch, setCandidatePage, setPage, setView, setSnapshotDialog, setTarget,
    setWillEat, setQuantityOnly, setQuantity, setReason }
}

function invalidSelection(quantityOnly: boolean, quantity: number, selectedBase: string, currentId: string | undefined): boolean {
  if (!Number.isInteger(quantity) || quantity < 1 || quantity > 200) return true
  return !quantityOnly && selectedBase !== currentId
}

export type PortionAmendmentModel = ReturnType<typeof usePortionAmendments>
