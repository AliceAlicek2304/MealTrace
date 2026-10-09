import { describe, expect, it } from 'vitest'
import { schoolDateTime, schoolTime, schoolToday } from './schoolTime'

describe('School timezone', () => {
  it('uses the school date across the UTC midnight boundary', () => {
    expect(schoolToday(new Date('2026-10-05T18:00:00Z'))).toBe('2026-10-06')
    expect(schoolTime('2026-10-06T00:30:00Z')).toBe('07:30')
    expect(schoolDateTime('2026-10-06T00:30:00Z')).toContain('07:30')
  })
  it('shows a placeholder for invalid timestamps', () => {
    expect(schoolDateTime('invalid')).toBe('—')
    expect(schoolTime('invalid')).toBe('—')
  })
})
