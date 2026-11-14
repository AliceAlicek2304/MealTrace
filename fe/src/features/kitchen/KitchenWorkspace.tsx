import { useState, type FormEvent } from 'react'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { Pagination } from '../../components/Pagination'
import { schoolToday, schoolTime } from '../../lib/schoolTime'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { DetailValues } from '../../components/DetailValues'
import { FilterPanel } from '../../components/FilterPanel'
import { QueryState } from '../../components/QueryState'

type Ingredient = {
  id: string
  name: string
  unit: string
  nutrition?: {
    versionId: string
    version: number
    energyKcalPer100G: number
    proteinGPer100G: number
    fatGPer100G?: number
    carbGPer100G?: number
    source?: string
  }
}
type Nutrition = {
  perServing: {
    kcal: number
    proteinG: number
    fatG: number | null
    carbG: number | null
  }
  incompleteData?: unknown[]
}
export function KitchenWorkspace() {
  const [search, setSearch] = useState(''),
    [page, setPage] = useState(1)
  const [edit, setEdit] = useState<Ingredient | 'new' | 'recipe' | null>(null),
    [busy, setBusy] = useState(false)
  const [recipeId, setRecipeId] = useState(''),
    [version, setVersion] = useState(1),
    [nutrition, setNutrition] = useState<Nutrition | null>(null)
  const [mode, setMode] = useState('recompute'),
    [lines, setLines] = useState<
      { ingredientId: string; name: string; unit: string; quantity: number }[]
    >([])
  const [recipeEditing, setRecipeEditing] = useState(false)
  const [pickerSearch, setPickerSearch] = useState('')
  const pickerTerm = useDebouncedValue(pickerSearch)
  const picker = useQuery({
    queryKey: ['ingredient-picker', pickerTerm],
    enabled: edit === 'recipe',
    queryFn: async () =>
      (
        await api.get<{ items: Ingredient[]; total: number }>(
          '/kitchen/ingredients',
          { params: { search: pickerTerm, pageSize: 25, page: 1 } },
        )
      ).data,
  })
  const searchTerm = useDebouncedValue(search)
  const ingredients = useQuery({
    queryKey: ['ingredient-catalog', searchTerm, page],
    queryFn: async () =>
      (
        await api.get<{ items: Ingredient[]; total: number }>(
          '/kitchen/ingredients',
          { params: { search: searchTerm, page, pageSize: 25 } },
        )
      ).data,
  })
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    const values = new FormData(event.currentTarget),
      text = (key: string) => {
        const value = values.get(key)
        return typeof value === 'string' ? value.trim() : ''
      },
      number = (key: string) => Number(text(key))
    setBusy(true)
    try {
      if (edit === 'new')
        await api.post('/kitchen/ingredients', {
          name: text('name'),
          unit: text('unit'),
        })
      else if (edit === 'recipe') {
        const payload = {
          name: text('name'),
          servingBasis: number('servingBasis'),
          lines: lines.map(({ ingredientId, quantity, unit }) => ({
            ingredientId,
            quantity,
            unit,
          })),
          expectedVersion: number('expectedVersion'),
          reason: text('reason') || undefined,
        }
        const response = await api.post<{ recipeId: string; version: number }>(
          text('existing')
            ? `/kitchen/recipes/${encodeURIComponent(text('existing'))}/versions`
            : '/kitchen/recipes',
          payload,
        )
        setRecipeId(response.data.recipeId)
        setVersion(response.data.version)
        setNutrition(null)
      } else if (edit)
        await api.post(`/kitchen/ingredients/${edit.id}/ingredient-versions`, {
          validFrom: `${text('validFrom')}:00+07:00`,
          supersedesId: values.has('supersede')
            ? edit.nutrition?.versionId
            : undefined,
          ediblePercent: number('ediblePercent'),
          energyKcal: number('energyKcal'),
          proteinG: number('proteinG'),
          fatG: number('fatG'),
          carbG: number('carbG'),
          source: text('source'),
          sourceRef: text('sourceRef') || null,
          gramsPerMl: text('gramsPerMl') ? number('gramsPerMl') : null,
        })
      setEdit(null)
      toast.success('Đã lưu dữ liệu.')
      await ingredients.refetch()
    } catch (error) {
      toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' })
    } finally {
      setBusy(false)
    }
  }
  async function lookup() {
    if (busy) return
    setBusy(true)
    setNutrition(null)
    try {
      setNutrition(
        (
          await api.get<Nutrition>(
            `/kitchen/recipes/${encodeURIComponent(recipeId)}/versions/${version}/nutrition`,
            { params: { mode } },
          )
        ).data,
      )
    } catch (error) {
      toast.error(apiErrorMessage(error))
    } finally {
      setBusy(false)
    }
  }
  const field = (
    key: string,
    label: string,
    type = 'text',
    initial = '',
    required = true,
  ) => (
    <label className="field" key={key}>
      {label}
      <input
        name={key}
        type={type}
        defaultValue={initial}
        required={required}
        disabled={busy}
        maxLength={type === 'text' ? 500 : undefined}
        min={type === 'number' ? 0 : undefined}
        step={type === 'number' ? 'any' : undefined}
      />
    </label>
  )
  function editorTitle() {
    if (edit === 'new') return 'Thêm nguyên liệu'
    if (edit === 'recipe') return 'Công thức'
    return edit ? `Dinh dưỡng: ${edit.name}` : ''
  }
  function editorFields() {
    if (edit === 'new')
      return (
        <>
          {field('name', 'Tên nguyên liệu')}
          <label className="field">
            <span>Đơn vị</span>
            <select name="unit">
              <option value="g">g</option>
              <option value="ml">ml</option>
            </select>
          </label>
        </>
      )
    if (edit === 'recipe')
      return (
        <>
          <label className="field">
            <span>Thao tác công thức</span>
            <select
              value={recipeEditing ? 'version' : 'new'}
              disabled={busy}
              onChange={(e) => setRecipeEditing(e.target.value === 'version')}
            >
              <option value="new">Tạo công thức mới</option>
              <option value="version">Thêm phiên bản công thức</option>
            </select>
          </label>
          {recipeEditing ? (
            <>
              {field('existing', 'Mã công thức', 'text', recipeId)}
              {field(
                'expectedVersion',
                'Phiên bản đối chiếu',
                'number',
                String(version),
              )}
            </>
          ) : (
            field('name', 'Tên công thức')
          )}
          {field('servingBasis', 'Số suất', 'number', '1')}
          {field('reason', 'Lý do phiên bản mới', 'text', '', false)}
          <label className="field">
            <span>Tìm thành phần</span>
            <input
              value={pickerSearch}
              onChange={(e) => setPickerSearch(e.target.value)}
              disabled={busy}
            />
          </label>
          <QueryState
            loading={picker.isPending}
            error={picker.isError}
            loadingMessage="Đang tìm nguyên liệu…"
            errorMessage="Không tải được nguyên liệu."
            onRetry={() => {
              void picker.refetch()
            }}
          >
            {null}
          </QueryState>
          <label className="field">
            <span>Thêm thành phần</span>
            <select
              value=""
              onChange={(e) => {
                const item = picker.data?.items.find(
                  (x) => x.id === e.target.value,
                )
                if (item && !lines.some((x) => x.ingredientId === item.id))
                  setLines([
                    ...lines,
                    {
                      ingredientId: item.id,
                      name: item.name,
                      unit: item.unit,
                      quantity: 1,
                    },
                  ])
              }}
            >
              <option value="">Chọn nguyên liệu</option>
              {picker.data?.items.map((x) => (
                <option value={x.id} key={x.id}>
                  {x.name} ({x.unit})
                </option>
              ))}
            </select>
          </label>
          {lines.map((line, index) => (
            <div key={line.ingredientId}>
              <label className="field">
                <span>
                  {line.name} — tổng lượng ({line.unit})
                </span>
                <input
                  required
                  type="number"
                  min="0.001"
                  step="any"
                  value={line.quantity}
                  onChange={(e) =>
                    setLines(
                      lines.map((x, i) =>
                        i === index
                          ? { ...x, quantity: Number(e.target.value) }
                          : x,
                      ),
                    )
                  }
                />
              </label>
              <button
                type="button"
                className="button secondary"
                onClick={() => setLines(lines.filter((_, i) => i !== index))}
              >
                Bỏ thành phần
              </button>
            </div>
          ))}
        </>
      )
    if (edit)
      return (
        <>
          {field(
            'validFrom',
            'Ngày bắt đầu áp dụng',
            'datetime-local',
            `${schoolToday()}T${schoolTime(new Date().toISOString())}`,
          )}
          {field('ediblePercent', 'Tỷ lệ ăn được (%)', 'number', '100')}
          {field('energyKcal', 'Năng lượng / 100g (kcal)', 'number')}
          {field('proteinG', 'Đạm / 100g (g)', 'number')}
          {field('fatG', 'Béo / 100g (g)', 'number')}
          {field('carbG', 'Carbohydrate / 100g (g)', 'number')}
          {field('source', 'Nguồn dữ liệu')}
          {edit.nutrition?.versionId && (
            <label className="checkbox-field">
              <input type="checkbox" name="supersede" disabled={busy} />
              <span>Thay thế bản dinh dưỡng đang áp dụng</span>
            </label>
          )}
          {field('sourceRef', 'Tài liệu tham chiếu', 'text', '', false)}
          {field(
            'gramsPerMl',
            'Khối lượng g/ml (nguyên liệu lỏng)',
            'number',
            '',
            false,
          )}
        </>
      )
    return null
  }
  return (
    <>
      <h1>Nguyên liệu & công thức</h1>
      <section className="panel workflow-lists">
        <div className="panel-head">
          <h2>Nguyên liệu</h2>
          <button
            type="button"
            className="button primary"
            onClick={() => setEdit('new')}
          >
            Thêm nguyên liệu
          </button>
        </div>
        <FilterPanel activeCount={search ? 1 : 0}>
          <div className="list-toolbar">
            <label className="field">
              <span>Tìm nguyên liệu</span>
              <input
                value={search}
                onChange={(e) => {
                  setSearch(e.target.value)
                  setPage(1)
                }}
              />
            </label>
          </div>
        </FilterPanel>
        <QueryState
          loading={ingredients.isPending}
          error={ingredients.isError}
          loadingMessage="Đang tải…"
          errorMessage="Không tải được nguyên liệu."
        >
          <div className="table-wrap">
            <ResponsiveTable>
              <thead>
                <tr>
                  <th>Tên</th>
                  <th>Đơn vị</th>
                  <th>Phiên bản</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {ingredients.data?.items.map((item) => (
                  <tr key={item.id}>
                    <td>{item.name}</td>
                    <td>{item.unit}</td>
                    <td>{item.nutrition?.version ?? 'Chưa có dinh dưỡng'}</td>
                    <td>
                      <button
                        type="button"
                        className="button secondary"
                        onClick={() => setEdit(item)}
                      >
                        Dinh dưỡng
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </ResponsiveTable>
            {ingredients.data?.items.length === 0 && (
              <p className="empty-state">Không có nguyên liệu phù hợp.</p>
            )}
          </div>
        </QueryState>
        <Pagination
          page={page}
          total={ingredients.data?.total ?? 0}
          pageSize={25}
          onChange={setPage}
        />
      </section>
      <section className="panel workflow-lists">
        <div className="panel-head">
          <h2>Công thức</h2>
          <button
            type="button"
            className="button primary"
            onClick={() => {
              setLines([])
              setRecipeEditing(Boolean(recipeId))
              setEdit('recipe')
            }}
          >
            Tạo / thêm phiên bản
          </button>
        </div>
        <form
          className="workflow-form"
          onSubmit={(e) => {
            e.preventDefault()
            void lookup()
          }}
        >
          <label className="field">
            <span>Mã công thức</span>
            <input
              required
              value={recipeId}
              onChange={(e) => {
                setRecipeId(e.target.value)
                setNutrition(null)
              }}
            />
          </label>
          <label className="field">
            <span>Phiên bản</span>
            <input
              required
              type="number"
              min="1"
              value={version}
              onChange={(e) => {
                setVersion(Number(e.target.value))
                setNutrition(null)
              }}
            />
          </label>
          <label className="field">
            <span>Dữ liệu tính</span>
            <select
              value={mode}
              onChange={(e) => {
                setMode(e.target.value)
                setNutrition(null)
              }}
            >
              <option value="recompute">Tính theo dữ liệu hiện hành</option>
              <option value="pinned">Theo phiên bản đã lưu</option>
            </select>
          </label>
          <button className="button secondary" type="submit" disabled={busy}>
            Xem dinh dưỡng
          </button>
          {nutrition && (
            <>
              <dl>
                {Object.entries(nutrition.perServing).map(([key, value]) => (
                  <div key={key}>
                    <dt>
                      {(
                        {
                          kcal: 'Năng lượng (kcal)',
                          proteinG: 'Đạm (g)',
                          fatG: 'Béo (g)',
                          carbG: 'Carbohydrate (g)',
                        } as Record<string, string>
                      )[key] ?? key}
                    </dt>
                    <dd>{value ?? 'Chưa đủ dữ liệu'}</dd>
                  </div>
                ))}
              </dl>
              <details>
                <summary>Chi tiết thành phần và dữ liệu tính</summary>
                <DetailValues value={nutrition} />
              </details>
            </>
          )}
        </form>
      </section>
      {edit && (
        <Modal title={editorTitle()} busy={busy} onClose={() => setEdit(null)}>
          <form className="workflow-form" onSubmit={save}>
            {editorFields()}
            <button
              type="submit"
              className="button primary"
              disabled={busy || (edit === 'recipe' && !lines.length)}
            >
              Lưu
            </button>
          </form>
        </Modal>
      )}
    </>
  )
}
