// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { currentSettlements, MealSettlementTables, type MealSettlement } from './MealSettlementTables'

afterEach(cleanup)
const row = (id: string, classId: string | null, version: number, count: number, settledAt = '2026-10-06T00:30:00Z'): MealSettlement => ({
  id, classId, className: classId, version, count, settledAt, settledBy: 'Admin', reason: version > 1 ? 'Đón sớm' : null, supersedesId: version > 1 ? 'original' : null,
})

describe('Meal settlement views', () => {
  it('shows one current snapshot per class and labels every version in history without adding old counts', () => {
    const rows = [row('original', 'M1', 1, 3), row('new', 'M1', 2, 2), row('second', 'M2', 1, 1)]
    render(<MealSettlementTables rows={rows} />)
    const current = screen.getByRole('table', { name: 'Suất hiện hành' })
    expect(within(current).getAllByRole('row')).toHaveLength(3)
    const m1 = within(current).getByText('M1').closest('tr')!
    expect(within(m1).getByText('Bản 2')).toBeTruthy()
    expect(within(m1).getByText('2')).toBeTruthy()
    expect(within(current).queryByText('3')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Lịch sử bản chốt (3)' }))
    const history = screen.getByRole('table', { name: 'Lịch sử bản chốt' })
    expect(within(history).getAllByRole('row')).toHaveLength(4)
    expect(within(history).getByText('3')).toBeTruthy()
    expect(within(history).getByText('Lịch sử')).toBeTruthy()
    expect(within(history).getByText('Đón sớm')).toBeTruthy()
    expect(screen.getByText(/không cộng dồn/)).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Suất hiện hành' }))
    expect(screen.queryByRole('table', { name: 'Lịch sử bản chốt' })).toBeNull()
  })

  it('selects the highest version independently of API ordering or identical timestamps', () => {
    expect(currentSettlements([row('new', 'M1', 3, 0), row('old', 'M1', 1, 4), row('middle', 'M1', 2, 2)]).map(x => x.id)).toEqual(['new'])
  })

  it('uses the latest school-wide legacy snapshot only when there are no class snapshots', () => {
    const old = row('old', null, 1, 5, '2026-10-05T00:30:00Z')
    const latest = row('latest', null, 1, 4)
    expect(currentSettlements([old, latest]).map(x => x.id)).toEqual(['latest'])
    expect(currentSettlements([latest, row('class', 'M1', 1, 2)]).map(x => x.id)).toEqual(['class'])
  })

  it('shows an empty state for an unsettled day', () => {
    render(<MealSettlementTables rows={[]} />)
    expect(screen.getByText('Chưa có bản chốt.')).toBeTruthy()
    expect(currentSettlements([])).toEqual([])
  })
})
