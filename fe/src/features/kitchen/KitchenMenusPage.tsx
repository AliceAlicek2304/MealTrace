import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { FilterPanel } from '../../components/FilterPanel'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { SectionSwitcher } from '../../components/SectionSwitcher'
import { schoolDateTime, schoolToday } from '../../lib/schoolTime'
import { MenuEditor } from './MenuEditor'
import { MenuEstimate } from './MenuEstimate'
import { MenuNutritionView } from './MenuNutritionView'
import { DataState, DateFields, dateLabel, fieldText, mealLabel, operationToast, optionalText } from './OperationsControls'
import { operationsApi as ops, pageSize, type DateRange, type Menu, type MenuState, type MenuVersion } from './operationsApi'

const states: Record<MenuState, string> = { draft: 'Bản nháp', submitted: 'Chờ duyệt', approved: 'Đã duyệt', rejected: 'Bị từ chối', published: 'Đã công bố' }

export function KitchenMenusPage({ isAdmin }: Readonly<{ isAdmin: boolean }>) {
  const [range, setRange] = useState<DateRange>(() => ({ from: schoolToday(), to: '' }))
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string | null>(null)
  const [creating, setCreating] = useState(false)
  const [busy, setBusy] = useState(false)
  const validRange = !range.from || !range.to || range.from <= range.to
  const query = useQuery({ queryKey: ['ops-menus', range, page], enabled: validRange, queryFn: ({ signal }) => ops.menus(range, page, signal) })
  function filter(value: DateRange) { setRange(value); setPage(1) }

  return <section><h1>Thực đơn</h1>
    <div className="panel workflow-lists">
      <div className="panel-head"><h2>Danh sách thực đơn</h2><button type="button" className="button primary" onClick={() => setCreating(true)}>Tạo thực đơn</button></div>
      <FilterPanel activeCount={Number(!!range.from) + Number(!!range.to)}><div className="operations-filter">
        <DateFields value={range} onChange={filter} />
        <button type="button" className="button secondary" onClick={() => filter({ from: '', to: '' })}>Tất cả ngày</button>
      </div></FilterPanel>
      {validRange ? <DataState query={query}><div className="table-wrap"><ResponsiveTable>
        <thead><tr><th>Ngày / bữa</th><th>Trạng thái</th><th>Phiên bản</th><th>Thao tác</th></tr></thead>
        <tbody>{query.data?.items.map(menu => <tr key={menu.id}>
          <td><strong>{mealLabel(menu.mealType)}</strong><small>{dateLabel(menu.date)}</small></td>
          <td><MenuStatus state={menu.latest.state} />{menu.publishedVersion && menu.publishedVersion !== menu.latest.version && <small>Đang công bố bản {menu.publishedVersion}</small>}</td>
          <td>Bản {menu.latest.version} · {menu.latest.dishes.length} món</td>
          <td><button type="button" className="button secondary" onClick={() => setSelected(menu.id)}>Chi tiết</button></td>
        </tr>)}
        {!query.data?.items.length && <tr><td colSpan={4}>Chưa có thực đơn trong khoảng ngày này.</td></tr>}</tbody>
      </ResponsiveTable></div></DataState> : <p className="empty compact" role="alert">Đến ngày phải từ ngày bắt đầu trở đi.</p>}
      {query.data && <Pagination page={page} total={query.data.total} pageSize={pageSize} busy={query.isFetching} onChange={setPage} />}
    </div>
    {creating && <Modal title="Tạo thực đơn" wide busy={busy} onClose={() => setCreating(false)}><MenuEditor onBusy={setBusy} onCancel={() => setCreating(false)} onSaved={id => { setCreating(false); setSelected(id) }} /></Modal>}
    {selected && <MenuDialog key={selected} id={selected} isAdmin={isAdmin} onClose={() => setSelected(null)} />}
  </section>
}

type View = 'overview' | 'history' | 'nutrition' | 'estimate'
const basicViews: { value: View; label: string }[] = [{ value: 'overview', label: 'Chi tiết' }, { value: 'history', label: 'Lịch sử' }]
const publicationViews: { value: View; label: string }[] = [{ value: 'nutrition', label: 'Dinh dưỡng' }, { value: 'estimate', label: 'Dự trù' }]

function MenuDialog({ id, isAdmin, onClose }: Readonly<{ id: string; isAdmin: boolean; onClose: () => void }>) {
  const [view, setView] = useState<View>('overview')
  const [editing, setEditing] = useState<Menu | null>(null)
  const [busy, setBusy] = useState(false)
  const query = useQuery({ queryKey: ['ops-menu', id], queryFn: ({ signal }) => ops.menu(id, signal) })
  const menu = query.data
  return <Modal wide busy={busy} title={editing ? 'Tạo phiên bản thực đơn' : menu ? `${mealLabel(menu.mealType)} · ${dateLabel(menu.date)}` : 'Chi tiết thực đơn'} onClose={onClose}>
    {editing ? <MenuEditor menu={editing} onBusy={setBusy} onCancel={() => setEditing(null)} onSaved={() => setEditing(null)} /> :
      <DataState query={query}>{menu && <div className="workflow-form">
        <fieldset className="operations-switch" disabled={busy}>
          <SectionSwitcher label="Thông tin thực đơn" value={view} onChange={setView} options={menu.publishedVersion ? [...basicViews, ...publicationViews] : basicViews} />
        </fieldset>
        {view === 'overview' && <MenuOverview key={menu.latest.versionId + menu.latest.state} menu={menu} isAdmin={isAdmin} busy={busy} onBusy={setBusy} onEdit={() => setEditing(menu)} />}
        {view === 'history' && <MenuHistory id={id} />}
        {view === 'nutrition' && menu.publishedVersion && <MenuNutritionView id={id} publishedVersion={menu.publishedVersion} />}
        {view === 'estimate' && menu.publishedVersionId && <MenuEstimate key={menu.publishedVersionId} menu={menu} busy={busy} onBusy={setBusy} />}
      </div>}</DataState>}
  </Modal>
}

function MenuStatus({ state }: Readonly<{ state: MenuState }>) {
  return <span className={`status ${state === 'published' || state === 'approved' ? 'active' : ''}`}>{states[state] ?? state}</span>
}
function MenuDishes({ version }: Readonly<{ version: MenuVersion }>) {
  return <ul className="operations-items">{version.dishes.map(dish => <li key={dish.recipeVersionId}>
    <strong>{dish.recipeName}</strong><small>Bản công thức {dish.recipeVersion}{dish.plannedPortions !== null ? ` · ${dish.plannedPortions} suất dự kiến` : ''}</small>
  </li>)}</ul>
}

type Transition = 'submit' | 'approve' | 'reject' | 'publish'
const actionLabels: Record<Transition, string> = { submit: 'Gửi duyệt', approve: 'Duyệt', reject: 'Từ chối', publish: 'Công bố' }

function MenuOverview({ menu, isAdmin, busy, onBusy, onEdit }: Readonly<{
  menu: Menu; isAdmin: boolean; busy: boolean; onBusy: (busy: boolean) => void; onEdit: () => void
}>) {
  const [action, setAction] = useState<Transition | null>(null)
  const cache = useQueryClient()
  const { id, latest } = menu
  const { version: expectedVersion, state } = latest
  const mutation = useMutation({
    mutationFn: ({ transition, reason, ageGroup }: { transition: Transition; reason: string; ageGroup: string }) => {
      if (transition === 'submit') return ops.submitMenu(id, expectedVersion)
      if (transition === 'publish') return ops.publishMenu(id, { expectedVersion, ageGroup: optionalText(ageGroup) })
      return ops.reviewMenu(id, { expectedVersion, decision: transition, reason: optionalText(reason) })
    },
    onMutate: () => { onBusy(true) },
    onSuccess: async () => {
      setAction(null)
      await Promise.all([
        cache.invalidateQueries({ queryKey: ['ops-menus'] }), cache.invalidateQueries({ queryKey: ['ops-menu', id] }),
        cache.invalidateQueries({ queryKey: ['ops-menu-history', id] }), cache.invalidateQueries({ queryKey: ['ops-parent-menus'] }),
      ])
      toast.success('Đã cập nhật thực đơn.', { toasterId: 'edit-modal' })
    },
    onError: operationToast,
    onSettled: () => { onBusy(false) },
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!action || busy) return
    const data = new FormData(event.currentTarget)
    const reason = fieldText(data, 'reason')
    if (action === 'reject' && !reason) { toast.error('Vui lòng nhập lý do từ chối.', { toasterId: 'edit-modal' }); return }
    mutation.mutate({ transition: action, reason, ageGroup: fieldText(data, 'ageGroup') })
  }
  const lastTransition = latest.transitions.at(-1)
  return <>
    <div className="entry"><strong>Bản {latest.version} · <MenuStatus state={state} /></strong>
      {menu.publishedVersion && menu.publishedVersion !== latest.version && <p>Phụ huynh đang xem bản {menu.publishedVersion}.</p>}
      {latest.reason && <p>Ghi chú bản này: {latest.reason}</p>}
      {lastTransition?.reason && <p>Lý do {states[lastTransition.state].toLowerCase()}: {lastTransition.reason}</p>}
    </div>
    <MenuDishes version={latest} />
    {action ? <form className="workflow-form entry" onSubmit={submit}>
      <h3>{actionLabels[action]} bản {expectedVersion}</h3>
      {(action === 'approve' || action === 'reject') && <label className="field"><span>{action === 'reject' ? 'Lý do từ chối' : 'Ghi chú duyệt'}</span><textarea name="reason" maxLength={500} required={action === 'reject'} /></label>}
      {action === 'publish' && <label className="field"><span>Nhóm tuổi để đối chiếu (tùy chọn)</span><input name="ageGroup" maxLength={50} /></label>}
      <div className="form-actions"><button type="button" disabled={busy} className="button secondary" onClick={() => setAction(null)}>Quay lại</button><button type="submit" disabled={busy} className="button primary">{actionLabels[action]}</button></div>
    </form> : <div className="form-actions">
      {state !== 'submitted' && <button type="button" className="button secondary" disabled={busy} onClick={onEdit}>Tạo bản mới</button>}
      {state === 'draft' && <button type="button" className="button primary" disabled={busy} onClick={() => setAction('submit')}>Gửi duyệt</button>}
      {isAdmin && state === 'submitted' && <><button type="button" className="button secondary" disabled={busy} onClick={() => setAction('reject')}>Từ chối</button><button type="button" className="button primary" disabled={busy} onClick={() => setAction('approve')}>Duyệt</button></>}
      {isAdmin && state === 'approved' && <button type="button" className="button primary" disabled={busy} onClick={() => setAction('publish')}>Công bố</button>}
    </div>}
    {!action && <button type="button" className="button secondary operations-inline" disabled={busy} onClick={() => void cache.invalidateQueries({ queryKey: ['ops-menu', id] })}>Tải lại thực đơn</button>}
  </>
}

function MenuHistory({ id }: Readonly<{ id: string }>) {
  const query = useQuery({ queryKey: ['ops-menu-history', id], queryFn: ({ signal }) => ops.menuHistory(id, signal) })
  return <DataState query={query}>{query.data?.items.map(version => <details className="entry" key={version.versionId}>
    <summary>Bản {version.version} · {states[version.state]}</summary>
    <small>{schoolDateTime(version.recordedAt)}</small>
    {version.reason && <p>Lý do tạo bản: {version.reason}</p>}
    <MenuDishes version={version} />
    <ul className="operations-items">{version.transitions.map(step => <li key={step.sequence}>
      <strong>{states[step.state]}</strong><small>{schoolDateTime(step.at)}</small>{step.reason && <p>{step.reason}</p>}
    </li>)}</ul>
  </details>)}</DataState>
}
