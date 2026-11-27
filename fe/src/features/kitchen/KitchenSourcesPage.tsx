import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { FilterPanel } from '../../components/FilterPanel'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { SectionSwitcher } from '../../components/SectionSwitcher'
import { schoolDateTime, schoolToday } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { CatalogPicker, DataState, DateFields, dateLabel, fieldText, numberLabel, operationToast, optionalText } from './OperationsControls'
import { operationsApi as ops, pageSize, type DateRange, type IngredientChoice, type Receipt, type Supplier } from './operationsApi'

const sections = [{ value: 'receipts', label: 'Phiếu nguồn' }, { value: 'suppliers', label: 'Nhà cung cấp' }] as const
const ingredientChoice = (item: IngredientChoice) => ({ id: item.id, label: item.name })
const supplierChoice = (item: Supplier) => ({ id: item.versionId, label: `${item.name} · Bản ${item.version}` })

export function KitchenSourcesPage() {
  const [section, setSection] = useState<'receipts' | 'suppliers'>('receipts')
  return <section><h1>Nguồn nguyên liệu</h1>
    <SectionSwitcher label="Danh mục" value={section} options={[...sections]} onChange={setSection} />
    {section === 'receipts' ? <Receipts /> : <Suppliers />}
  </section>
}

function Suppliers() {
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())
  const [page, setPage] = useState(1)
  const [opened, setOpened] = useState<Supplier | 'new' | null>(null)
  const query = useQuery({ queryKey: ['ops-suppliers', term, page], queryFn: ({ signal }) => ops.suppliers(term, page, signal) })
  return <div className="panel workflow-lists">
    <div className="panel-head"><h2>Nhà cung cấp</h2><button type="button" className="button primary" onClick={() => setOpened('new')}>Thêm nhà cung cấp</button></div>
    <FilterPanel activeCount={search ? 1 : 0}><div className="list-toolbar"><label className="field"><span>Tìm nhà cung cấp</span><input value={search} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label></div></FilterPanel>
    <DataState query={query}><div className="table-wrap"><ResponsiveTable>
      <thead><tr><th>Nhà cung cấp</th><th>Liên hệ</th><th>Phiên bản</th><th>Thao tác</th></tr></thead>
      <tbody>{query.data?.items.map(item => <tr key={item.id}><td><strong>{item.name}</strong></td><td>{item.contact || 'Chưa có'}</td><td>Bản {item.version}</td><td><button type="button" className="button secondary" onClick={() => setOpened(item)}>Chi tiết</button></td></tr>)}
        {!query.data?.items.length && <tr><td colSpan={4}>Chưa có nhà cung cấp phù hợp.</td></tr>}</tbody>
    </ResponsiveTable></div></DataState>
    {query.data && <Pagination page={page} total={query.data.total} pageSize={pageSize} busy={query.isFetching} onChange={setPage} />}
    {opened && <SupplierDialog key={opened === 'new' ? 'new' : opened.id} item={opened === 'new' ? null : opened} onClose={() => setOpened(null)} />}
  </div>
}

function SupplierDialog({ item, onClose }: Readonly<{ item: Supplier | null; onClose: () => void }>) {
  const [editing, setEditing] = useState(!item)
  const cache = useQueryClient()
  const history = useQuery({ queryKey: ['ops-supplier-history', item?.id], enabled: !!item && !editing, queryFn: ({ signal }) => ops.supplierHistory(item!.id, signal) })
  const save = useMutation({
    mutationFn: (form: FormData) => {
      const values = { name: fieldText(form, 'name'), contact: optionalText(fieldText(form, 'contact')), reference: optionalText(fieldText(form, 'reference')) }
      return item ? ops.reviseSupplier(item.id, { ...values, expectedVersion: item.version, reason: fieldText(form, 'reason') }) : ops.createSupplier(values)
    },
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ['ops-suppliers'] }); await cache.invalidateQueries({ queryKey: ['ops-supplier-history', item?.id] }); onClose(); toast.success('Đã lưu nhà cung cấp.') },
    onError: operationToast,
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (save.isPending) return
    const form = new FormData(event.currentTarget)
    if (!fieldText(form, 'name') || (item && !fieldText(form, 'reason'))) {
      toast.error('Vui lòng nhập tên nhà cung cấp và lý do nếu đang sửa.', { toasterId: 'edit-modal' })
      return
    }
    save.mutate(form)
  }
  return <Modal title={editing ? (item ? 'Sửa nhà cung cấp' : 'Thêm nhà cung cấp') : item!.name} busy={save.isPending} onClose={onClose}>
    {editing ? <form className="workflow-form" onSubmit={submit}>
      <label className="field"><span>Tên nhà cung cấp</span><input required name="name" maxLength={200} defaultValue={item?.name} /></label>
      <label className="field"><span>Liên hệ</span><input name="contact" maxLength={500} defaultValue={item?.contact ?? ''} /></label>
      <label className="field"><span>Mã tham chiếu</span><input name="reference" maxLength={100} defaultValue={item?.reference ?? ''} /></label>
      {item && <label className="field"><span>Lý do sửa</span><textarea required name="reason" maxLength={500} /></label>}
      <div className="form-actions"><button type="button" className="button secondary" onClick={item ? () => setEditing(false) : onClose} disabled={save.isPending}>Quay lại</button><button type="submit" className="button primary" disabled={save.isPending}>Lưu</button></div>
    </form> : <div className="workflow-form">
      <div className="form-actions"><button type="button" className="button primary" onClick={() => setEditing(true)}>Sửa nhà cung cấp</button></div>
      <h3>Lịch sử phiên bản</h3>
      <DataState query={history}>{history.data?.items.map(version => <article key={version.versionId} className="entry">
        <strong>Bản {version.version} · {version.name}</strong><small>{schoolDateTime(version.recordedAt)}</small>
        <p>Liên hệ: {version.contact || 'Chưa có'} · Mã tham chiếu: {version.reference || 'Chưa có'}</p>{version.reason && <p>Lý do: {version.reason}</p>}
      </article>)}</DataState>
    </div>}
  </Modal>
}

function Receipts() {
  const [range, setRange] = useState<DateRange>({ from: '', to: '' })
  const [ingredient, setIngredient] = useState<IngredientChoice | null>(null)
  const [page, setPage] = useState(1)
  const [opened, setOpened] = useState<Receipt | 'new' | null>(null)
  const validRange = !range.from || !range.to || range.from <= range.to
  const query = useQuery({ queryKey: ['ops-receipts', range, ingredient?.id ?? '', page], enabled: validRange, queryFn: ({ signal }) => ops.receipts(range, ingredient?.id ?? '', page, signal) })
  return <div className="panel workflow-lists">
    <div className="panel-head"><h2>Phiếu nguồn</h2><button type="button" className="button primary" onClick={() => setOpened('new')}>Thêm phiếu nguồn</button></div>
    <FilterPanel activeCount={Number(!!range.from) + Number(!!range.to) + Number(!!ingredient)}><div className="operations-filter">
      <DateFields value={range} onChange={value => { setRange(value); setPage(1) }} />
      <CatalogPicker label="Nguyên liệu" prefix={['ops-ingredients']} value={ingredient && ingredientChoice(ingredient)} options={ops.ingredients} toChoice={ingredientChoice} onChange={value => { setIngredient(value); setPage(1) }} />
    </div></FilterPanel>
    {validRange ? <DataState query={query}><div className="table-wrap"><ResponsiveTable>
      <thead><tr><th>Nguyên liệu</th><th>Nhà cung cấp</th><th>Ngày nhận</th><th>Số lượng</th><th>Thao tác</th></tr></thead>
      <tbody>{query.data?.items.map(item => <tr key={item.id}><td><strong>{item.ingredientName}</strong></td><td>{item.supplierName}</td><td>{dateLabel(item.referenceDate)}</td><td>{numberLabel(item.quantity)} {item.unit}</td><td><button type="button" className="button secondary" onClick={() => setOpened(item)}>Chi tiết</button></td></tr>)}
        {!query.data?.items.length && <tr><td colSpan={5}>Chưa có phiếu nguồn phù hợp.</td></tr>}</tbody>
    </ResponsiveTable></div></DataState> : <p className="empty compact" role="alert">Đến ngày phải từ ngày bắt đầu trở đi.</p>}
    {query.data && <Pagination page={page} total={query.data.total} pageSize={pageSize} busy={query.isFetching} onChange={setPage} />}
    {opened && <ReceiptDialog key={opened === 'new' ? 'new' : opened.id} item={opened === 'new' ? null : opened} onClose={() => setOpened(null)} />}
  </div>
}

function ReceiptDialog({ item, onClose }: Readonly<{ item: Receipt | null; onClose: () => void }>) {
  const [editing, setEditing] = useState(!item)
  const [ingredient, setIngredient] = useState<IngredientChoice | null>(item ? { id: item.ingredientId, name: item.ingredientName, unit: item.unit } : null)
  const [supplier, setSupplier] = useState<{ id: string; label: string } | null>(item ? { id: item.supplierVersionId, label: item.supplierName } : null)
  const cache = useQueryClient()
  const history = useQuery({ queryKey: ['ops-receipt-history', item?.id], enabled: !!item && !editing, queryFn: ({ signal }) => ops.receiptHistory(item!.id, signal) })
  const save = useMutation({
    mutationFn: (form: FormData) => {
      const values = {
        supplierVersionId: supplier!.id, referenceDate: fieldText(form, 'date'), quantity: Number(form.get('quantity')),
        unit: ingredient!.unit, deliveryRef: optionalText(fieldText(form, 'delivery')), lotRef: optionalText(fieldText(form, 'lot')),
      }
      return item ? ops.reviseReceipt(item.id, { ...values, expectedVersion: item.version, reason: fieldText(form, 'reason') }) : ops.createReceipt({ ...values, ingredientId: ingredient!.id })
    },
    onSuccess: async () => { await cache.invalidateQueries({ queryKey: ['ops-receipts'] }); await cache.invalidateQueries({ queryKey: ['ops-receipt-history', item?.id] }); onClose(); toast.success('Đã lưu phiếu nguồn.') },
    onError: operationToast,
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    if (save.isPending || !supplier || !ingredient) return
    if (!(Number(form.get('quantity')) > 0) || (item && !fieldText(form, 'reason'))) {
      toast.error('Kiểm tra số lượng và lý do sửa.', { toasterId: 'edit-modal' })
      return
    }
    save.mutate(form)
  }
  return <Modal title={editing ? (item ? 'Sửa phiếu nguồn' : 'Thêm phiếu nguồn') : item!.ingredientName} busy={save.isPending} onClose={onClose}>
    {editing ? <form className="workflow-form" onSubmit={submit}>
      {item ? <p>Nguyên liệu: <strong>{item.ingredientName}</strong></p> : <CatalogPicker label="Nguyên liệu" prefix={['ops-ingredients']} value={ingredient && ingredientChoice(ingredient)} options={ops.ingredients} toChoice={ingredientChoice} onChange={setIngredient} />}
      <CatalogPicker label="Nhà cung cấp" prefix={['ops-suppliers']} value={supplier} options={ops.suppliers} toChoice={supplierChoice} onChange={value => setSupplier(value && supplierChoice(value))} />
      <div className="form-grid">
        <label className="field"><span>Ngày nhận</span><input name="date" type="date" required defaultValue={item?.referenceDate ?? schoolToday()} /></label>
        <label className="field"><span>Số lượng {ingredient ? `(${ingredient.unit})` : ''}</span><input name="quantity" type="number" step="any" min="0" required defaultValue={item?.quantity} /></label>
        <label className="field"><span>Mã phiếu giao</span><input name="delivery" maxLength={100} defaultValue={item?.deliveryRef ?? ''} /></label>
        <label className="field"><span>Mã lô</span><input name="lot" maxLength={100} defaultValue={item?.lotRef ?? ''} /></label>
      </div>
      {item && <label className="field"><span>Lý do sửa</span><textarea name="reason" required maxLength={500} /></label>}
      <div className="form-actions"><button type="button" className="button secondary" onClick={item ? () => setEditing(false) : onClose} disabled={save.isPending}>Quay lại</button><button type="submit" className="button primary" disabled={!ingredient || !supplier || save.isPending}>Lưu</button></div>
    </form> : <div className="workflow-form">
      <div className="form-actions"><button type="button" className="button primary" onClick={() => setEditing(true)}>Sửa phiếu nguồn</button></div>
      <h3>Lịch sử phiên bản</h3>
      <DataState query={history}>{history.data?.items.map(version => <article className="entry" key={version.versionId}>
        <strong>Bản {version.version} · {version.supplierName}</strong><small>{schoolDateTime(version.recordedAt)}</small>
        <p>{dateLabel(version.referenceDate)} · {numberLabel(version.quantity)} {version.unit}</p>
        <p>Phiếu giao: {version.deliveryRef || 'Chưa có'} · Lô: {version.lotRef || 'Chưa có'}</p>{version.reason && <p>Lý do: {version.reason}</p>}
      </article>)}</DataState>
    </div>}
  </Modal>
}
