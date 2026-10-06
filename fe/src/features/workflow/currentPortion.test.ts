import { describe, expect, it } from 'vitest'
import { currentStudentPortion, type CurrentClassPortion } from './currentPortion'

const room: CurrentClassPortion = { classId: 'class', className: 'M1', studentIds: ['child-1', 'child-3'], isSettled: true, version: 2, count: 2, kitchenAdjustment: 0 }

describe('Current student portions', () => {
  it('shows the removed child without a portion and the remaining child with a portion', () => {
    expect(currentStudentPortion('child-2', room)).toBe(false)
    expect(currentStudentPortion('child-1', room)).toBe(true)
  })
  it('preserves child eligibility when only the kitchen quantity is reduced', () => {
    expect(currentStudentPortion('child-1', { ...room, count: 0, kitchenAdjustment: -2 })).toBe(true)
  })
  it('does not infer absence from incomplete historical rosters', () => {
    expect(currentStudentPortion('child-2', { ...room, count: 10 })).toBeNull()
  })
  it('does not claim a current portion before a snapshot is available', () => {
    expect(currentStudentPortion('child-1')).toBeNull()
    expect(currentStudentPortion('child-1', { ...room, isSettled: false })).toBeNull()
  })
})
