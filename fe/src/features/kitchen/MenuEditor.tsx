import { useState, type FormEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { Pagination } from '../../components/Pagination'
import { schoolToday } from '../../lib/schoolTime'
import { CatalogPicker, DataState, dateLabel, fieldText, mealLabel, operationToast, optionalText } from './OperationsControls'
import { operationsApi as ops, pageSize, type MealDayChoice, type Menu, type MenuDish, type Recipe } from './operationsApi'

type EditableDish = { recipeVersionId: string; recipeName: string; recipeVersion: number; portions: string }

export function MenuEditor({ menu, onCancel, onSaved, onBusy }: Readonly<{
  menu?: Menu; onCancel: () => void; onSaved: (id: string) => void; onBusy: (busy: boolean) => void
}>) {
  const [day, setDay] = useState<MealDayChoice | null>(null)
  const [dishes, setDishes] = useState<EditableDish[]>(() => menu?.latest.dishes.map(editableDish) ?? [])
  const cache = useQueryClient()
  const mutation = useMutation({
    mutationFn: async (reason: string) => {
      const payload = dishes.map(item => ({ recipeVersionId: item.recipeVersionId, plannedPortions: item.portions ? Number(item.portions) : null }))
      if (menu) {
        await ops.reviseMenu(menu.id, { expectedVersion: menu.latest.version, dishes: payload, reason })
        return menu.id
      }
      return (await ops.createMenu({ mealDayId: day!.id, dishes: payload, reason: optionalText(reason) })).id
    },
    onMutate: () => { onBusy(true) },
    onSuccess: async id => {
      await cache.invalidateQueries({ queryKey: ['ops-menus'] })
      await cache.invalidateQueries({ queryKey: ['ops-menu', id] })
      await cache.invalidateQueries({ queryKey: ['ops-menu-history', id] })
      onSaved(id)
      toast.success('Đã lưu bản nháp thực đơn.')
    },
    onError: operationToast,
    onSettled: () => { onBusy(false) },
  })
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (mutation.isPending || (!menu && !day) || !dishes.length) return
    const reason = fieldText(new FormData(event.currentTarget), 'reason')
    if (menu && !reason) { toast.error('Vui lòng nhập lý do tạo phiên bản mới.', { toasterId: 'edit-modal' }); return }
    mutation.mutate(reason)
  }

  return <form className="workflow-form" onSubmit={submit}>
    {!menu && <MealDayPicker day={day} onChange={setDay} />}
    <h3>Món ăn</h3>
    {dishes.map(dish => <article className="entry operations-dish" key={dish.recipeVersionId}>
      <strong>{dish.recipeName}<small>Bản công thức {dish.recipeVersion}</small></strong>
      <label className="field"><span>Số suất dự kiến</span><input type="number" min="1" max="100000" step="1" value={dish.portions} onChange={event => {
        const portions = event.target.value
        setDishes(items => items.map(item => item.recipeVersionId === dish.recipeVersionId ? { ...item, portions } : item))
      }} /></label>
      <button type="button" className="button secondary" aria-label={`Bỏ món ${dish.recipeName}`} onClick={() => setDishes(items => items.filter(item => item.recipeVersionId !== dish.recipeVersionId))}>Bỏ món</button>
    </article>)}
    {dishes.length < 30 && <RecipePicker used={dishes.map(dish => dish.recipeVersionId)} onAdd={dish => setDishes(items => [...items, dish])} />}
    <label className="field"><span>{menu ? 'Lý do tạo bản mới' : 'Ghi chú'}</span><textarea name="reason" required={!!menu} maxLength={500} /></label>
    <div className="form-actions"><button type="button" className="button secondary" disabled={mutation.isPending} onClick={onCancel}>Quay lại</button><button type="submit" className="button primary" disabled={mutation.isPending || (!menu && !day) || !dishes.length}>Lưu bản nháp</button></div>
  </form>
}

function editableDish(dish: MenuDish): EditableDish {
  return {
    recipeVersionId: dish.recipeVersionId, recipeName: dish.recipeName,
    recipeVersion: dish.recipeVersion, portions: dish.plannedPortions?.toString() ?? '',
  }
}

function MealDayPicker({ day, onChange }: Readonly<{ day: MealDayChoice | null; onChange: (day: MealDayChoice | null) => void }>) {
  const [date, setDate] = useState(schoolToday)
  const [page, setPage] = useState(1)
  const query = useQuery({ queryKey: ['ops-meal-days', date, page], enabled: !!date, queryFn: ({ signal }) => ops.mealDays(date, page, signal) })
  const items = query.data?.items.filter(item => !item.isCancelled) ?? []
  return <div className="operations-picker">
    <label className="field"><span>Ngày ăn</span><input type="date" required value={date} onChange={e => { setDate(e.target.value); onChange(null); setPage(1) }} /></label>
    {date && <DataState query={query}>
      <label className="field"><span>Phiên ăn</span><select value={day?.id ?? ''} onChange={e => onChange(items.find(item => item.id === e.target.value) ?? null)}>
        <option value="">Chọn phiên ăn</option>
        {day && !items.some(item => item.id === day.id) && <option value={day.id}>{mealLabel(day.mealType)}</option>}
        {items.map(item => <option key={item.id} value={item.id}>{mealLabel(item.mealType)} · {dateLabel(item.date)}</option>)}
      </select></label>
      {!items.length && <small>Ngày này chưa có phiên ăn có thể lập thực đơn.</small>}
      {query.data && query.data.total > query.data.pageSize && <Pagination page={page} total={query.data.total} pageSize={query.data.pageSize || pageSize} busy={query.isFetching} onChange={setPage} />}
    </DataState>}
  </div>
}

function RecipePicker({ used, onAdd }: Readonly<{ used: string[]; onAdd: (dish: EditableDish) => void }>) {
  const [recipe, setRecipe] = useState<Recipe | null>(null)
  const [versionId, setVersionId] = useState('')
  const query = useQuery({ queryKey: ['ops-recipe-versions', recipe?.recipeId], enabled: !!recipe, queryFn: ({ signal }) => ops.recipeVersions(recipe!.recipeId, signal) })
  const version = query.data?.items.find(item => item.versionId === versionId)
  return <div className="entry operations-picker">
    <CatalogPicker label="Công thức" prefix={['ops-recipes']} value={recipe && recipeChoice(recipe)} options={ops.recipes} toChoice={recipeChoice} onChange={item => { setRecipe(item); setVersionId(item?.versionId ?? '') }} />
    {recipe && <DataState query={query}>
      <label className="field"><span>Phiên bản công thức</span><select value={versionId} onChange={e => setVersionId(e.target.value)}>
        <option value="">Chọn phiên bản</option>
        {query.data?.items.map(item => <option key={item.versionId} value={item.versionId} disabled={used.includes(item.versionId)}>Bản {item.version}{item.lines.some(line => line.legacyConversionUnverified) ? ' · Chưa xác minh quy đổi' : ''}</option>)}
      </select></label>
      <button type="button" className="button secondary" disabled={!version || used.includes(version.versionId)} onClick={() => {
        if (!version) return
        onAdd({ recipeVersionId: version.versionId, recipeName: recipe.name, recipeVersion: version.version, portions: '' })
        setRecipe(null); setVersionId('')
      }}>Thêm món</button>
    </DataState>}
  </div>
}
const recipeChoice = (item: Recipe) => ({ id: item.recipeId, label: item.name })
