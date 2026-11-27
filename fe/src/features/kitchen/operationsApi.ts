import { api } from '../../lib/api'

export const pageSize = 20
export type PageResult<T> = { items: T[]; total: number; page: number; pageSize: number }
export type DateRange = { from: string; to: string }
export type IngredientChoice = { id: string; name: string; unit: string }
export type SupplierFields = { name: string; contact: string | null; reference: string | null }
type Revision = { versionId: string; version: number; recordedAt: string }
export type Supplier = SupplierFields & Revision & { id: string }
export type SupplierRevision = SupplierFields & Revision & { reason: string | null }
export type ReceiptFields = { supplierVersionId: string; referenceDate: string; quantity: number; unit: string; deliveryRef: string | null; lotRef: string | null }
export type Receipt = ReceiptFields & Revision & { id: string; ingredientId: string; ingredientName: string; supplierName: string }
export type ReceiptRevision = ReceiptFields & Revision & { reason: string | null; supplierName: string }
type Correction = { expectedVersion: number; reason: string }
export type MealDayChoice = { id: string; date: string; mealType: string; isCancelled: boolean }
export type Recipe = { recipeId: string; name: string; version: number; versionId: string }
export type RecipeRevision = Revision & { reason: string | null; lines: { ingredientId: string; ingredientName: string; gramsPerPortion: number; legacyConversionUnverified: boolean }[] }
export type DishInput = { recipeVersionId: string; plannedPortions: number | null }
export type MenuDish = DishInput & { recipeId: string; recipeName: string; recipeVersion: number }
export type MenuState = 'draft' | 'submitted' | 'approved' | 'rejected' | 'published'
export type MenuVersion = Revision & {
  state: MenuState; reason: string | null; dishes: MenuDish[]
  transitions: { sequence: number; state: MenuState; reason: string | null; at: string }[]
}
export type Menu = {
  id: string; mealDayId: string; date: string; mealType: string; latest: MenuVersion
  publishedVersionId: string | null; publishedVersion: number | null
}
export type Totals = { kcal: number; proteinG: number; fatG: number | null; carbG: number | null }
export type Comparison = { nutrient: string; value: number | null; min: number | null; max: number | null; status: string; source: string; version: number }
type NutritionPayload = {
  total: Totals; ageGroup: string | null; ageComparisonStatus: string
  ageComparison: { items: Comparison[] } | null
  dishes: (MenuDish & { perServing: Totals })[]
}
export type MenuNutrition = { version: number; payload: NutritionPayload; asPublished: NutritionPayload | null; deltaFromPublished: Totals | null }
export type PublishedMenu = {
  mealDayId: string; date: string; mealType: string; dishes: MenuDish[]
  nutrition: Totals | null; ageComparisonStatus: string
}
export type LinkedChild = { studentId: string; fullName: string; className: string }
export type Estimate = {
  id: string; createdAt: string
  portions: { mealDayId: string; classId: string; version: number; portions: number }[]
  lines: { ingredientId: string; ingredientName: string; quantity: number; unit: string; grossGrams: number; edibleGrams: number }[]
}
export type EstimateInput = { menuVersionIds: string[]; mealDayIds: string[]; classIds?: string[]; snapshotTokens: Record<string, string> }

async function get<T>(url: string, params?: object, signal?: AbortSignal): Promise<T> {
  return (await api.get<T>(url, { params, signal })).data
}
async function post<T>(url: string, body: object): Promise<T> {
  return (await api.post<T>(url, body)).data
}

const suppliers = '/kitchen/suppliers'
const receipts = '/kitchen/ingredient-source-entries'
const menus = '/kitchen/menus'
const recipes = '/kitchen/recipes'
const estimates = '/kitchen/ingredient-estimates'

export const operationsApi = {
  suppliers: (search: string, page: number, signal?: AbortSignal) => get<PageResult<Supplier>>(suppliers, { search, page, pageSize }, signal),
  supplierHistory: (id: string, signal?: AbortSignal) => get<{ items: SupplierRevision[] }>(`${suppliers}/${id}/history`, undefined, signal),
  createSupplier: (body: SupplierFields) => post(suppliers, body),
  reviseSupplier: (id: string, body: SupplierFields & Correction) => post(`${suppliers}/${id}/versions`, body),
  ingredients: (search: string, page: number, signal?: AbortSignal) => get<PageResult<IngredientChoice>>('/kitchen/ingredients', { search, page, pageSize }, signal),
  receipts: (range: DateRange, ingredientId: string, page: number, signal?: AbortSignal) =>
    get<PageResult<Receipt>>(receipts, { from: range.from || undefined, to: range.to || undefined, ingredientId: ingredientId || undefined, page, pageSize }, signal),
  receiptHistory: (id: string, signal?: AbortSignal) => get<{ items: ReceiptRevision[] }>(`${receipts}/${id}/history`, undefined, signal),
  createReceipt: (body: ReceiptFields & { ingredientId: string }) => post(receipts, body),
  reviseReceipt: (id: string, body: ReceiptFields & Correction) => post(`${receipts}/${id}/versions`, body),
  mealDays: (date: string, page: number, signal?: AbortSignal) =>
    get<PageResult<MealDayChoice>>('/meal-days/search', { date, page, pageSize }, signal),
  recipes: (search: string, page: number, signal?: AbortSignal) => get<PageResult<Recipe>>(recipes, { search, page, pageSize }, signal),
  recipeVersions: (id: string, signal?: AbortSignal) => get<{ name: string; items: RecipeRevision[] }>(`${recipes}/${id}/versions`, undefined, signal),
  menus: (range: DateRange, page: number, signal?: AbortSignal) =>
    get<PageResult<Menu>>(menus, { from: range.from || undefined, to: range.to || undefined, page, pageSize }, signal),
  menu: (id: string, signal?: AbortSignal) => get<Menu>(`${menus}/${id}`, undefined, signal),
  menuHistory: (id: string, signal?: AbortSignal) => get<{ items: MenuVersion[] }>(`${menus}/${id}/history`, undefined, signal),
  createMenu: (body: { mealDayId: string; dishes: DishInput[]; reason: string | null }) => post<{ id: string }>(menus, body),
  reviseMenu: (id: string, body: Correction & { dishes: DishInput[] }) => post(`${menus}/${id}/versions`, body),
  submitMenu: (id: string, expectedVersion: number) => post(`${menus}/${id}/submit`, { expectedVersion }),
  reviewMenu: (id: string, body: { expectedVersion: number; decision: 'approve' | 'reject'; reason: string | null }) => post(`/admin/menus/${id}/review`, body),
  publishMenu: (id: string, body: { expectedVersion: number; ageGroup: string | null }) => post(`/admin/menus/${id}/publish`, body),
  nutrition: (id: string, params: { version: number; mode: 'pinned' | 'recompute'; ageGroup?: string; validAt?: string; knownAt?: string }, signal?: AbortSignal) =>
    get<MenuNutrition>(`${menus}/${id}/nutrition`, params, signal),
  children: (signal?: AbortSignal) => get<LinkedChild[]>('/parent/students', undefined, signal),
  parentMenus: (studentId: string, range: DateRange, signal?: AbortSignal) =>
    get<{ items: PublishedMenu[] }>(`/parent/students/${studentId}/menus`, range, signal),
  createEstimate: (body: EstimateInput) => post<Estimate>(estimates, body),
  estimate: (id: string, signal?: AbortSignal) => get<Estimate>(`${estimates}/${id}`, undefined, signal),
  exportEstimate: (id: string) => api.get<Blob>(`${estimates}/${id}/export`, { params: { format: 'csv' }, responseType: 'blob' }),
}
