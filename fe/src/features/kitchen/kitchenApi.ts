import { api } from '../../lib/api'

export type KitchenClass = { classId: string; className: string; originalCount: number; appliedCount: number; diff: number; kitchenAdjustment: number; version: number }
export type KitchenSummary = { mealDayId: string; date: string; sessionType: string; originalPortions: number; appliedPortions: number; diff: number; isAcknowledged: boolean; classes: KitchenClass[] }
export type KitchenDiff = { mealDayId: string; originalTotal: number; appliedTotal: number; totalDiff: number; snapshotToken: string; isAcknowledged: boolean; classDiffs: (Omit<KitchenClass, 'version'> & { currentVersion: number; hasCompleteRoster: boolean; addedStudents: {studentId: string; studentName: string}[]; removedStudents: {studentId: string; studentName: string}[] })[] }
export type KitchenAcknowledgement = { id: string; acknowledgedAt: string; acknowledgedByName: string; portionsCount: number; note: string | null; snapshotToken: string | null }

export async function searchKitchenSettlements(date: string, page: number) {
  return (await api.get<{ items: KitchenSummary[]; total: number }>('/kitchen/settlements/search', { params: { date: date || undefined, page, pageSize: 20 } })).data
}
export async function kitchenDiff(id: string) { return (await api.get<KitchenDiff>(`/kitchen/settlements/${id}/diff`)).data }
export async function kitchenHistory(id: string) { return (await api.get<KitchenAcknowledgement[]>(`/kitchen/settlements/${id}/acknowledgements`)).data }
export async function acknowledgeKitchen(id: string, snapshotToken: string, note: string) {
  return api.post(`/kitchen/settlements/${id}/acknowledge`, { snapshotToken, note: note.trim() || null })
}
