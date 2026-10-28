import { describe, expect, it } from 'vitest'
import { earlierSchoolDate, laterSchoolDate, schoolDateTime, schoolTime, schoolToday, isUnavailableMealPeriod } from './schoolTime'

describe('School timezone', () => {
  it('blocks only periods entirely without school meals, including duplicate dates and month boundaries', () => {
    const closed = ['2026-10-31', '2026-11-01', '2026-11-01']
    expect(isUnavailableMealPeriod('2026-10-31', '2026-10-31', closed)).toBe(true)
    expect(isUnavailableMealPeriod('2026-10-31', '2026-11-01', closed)).toBe(true)
    expect(isUnavailableMealPeriod('2026-10-31', '2026-11-02', closed)).toBe(false)
    expect(isUnavailableMealPeriod('2026-10-31', '2026-11-01')).toBe(false)
    expect(isUnavailableMealPeriod('', '', closed)).toBe(false)
  })
  it('uses the school date across the UTC midnight boundary', () => {
    expect(schoolToday(new Date('2026-10-05T18:00:00Z'))).toBe('2026-10-06')
    expect(schoolTime('2026-10-06T00:30:00Z')).toBe('07:30')
    expect(schoolDateTime('2026-10-06T00:30:00Z')).toContain('07:30')
  })
  it('shows a placeholder for invalid timestamps', () => {
    expect(schoolDateTime('invalid')).toBe('—')
    expect(schoolTime('invalid')).toBe('—')
  })
  it('keeps ISO dates as strings when limiting a date range across month and year boundaries', () => {
    expect(earlierSchoolDate('2027-01-01', '2026-12-31')).toBe('2026-12-31')
    expect(laterSchoolDate('2026-09-30', '2026-10-01')).toBe('2026-10-01')
    expect(earlierSchoolDate('2026-10-01', '2026-10-01')).toBe('2026-10-01')
    expect(laterSchoolDate('2026-10-01', '2026-10-01')).toBe('2026-10-01')
  })
})
