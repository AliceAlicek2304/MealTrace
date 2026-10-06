export type CurrentClassPortion = {
  classId: string
  className: string
  studentIds: string[]
  isSettled: boolean
  version: number
  count: number | null
  kitchenAdjustment: number
}

// Quantity-only corrections affect kitchen totals, not individual eligibility.
// Incomplete historical rosters cannot establish that a missing child has no portion.
export function currentStudentPortion(studentId: string, room?: CurrentClassPortion): boolean | null {
  if (!room?.isSettled || room.count === null || room.count - room.kitchenAdjustment !== room.studentIds.length) return null
  return room.studentIds.includes(studentId)
}
