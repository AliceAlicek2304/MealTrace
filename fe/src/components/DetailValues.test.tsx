// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, it } from 'vitest'
import { DetailValues } from './DetailValues'

afterEach(cleanup)
it('keeps each audit record attached to its identity when entries reorder', () => {
  const first = { id: 'first', name: 'Trẻ An' },
    second = { id: 'second', name: 'Trẻ Bình' }
  const view = render(<DetailValues value={[first, second]} />)
  const original = screen.getByText('Trẻ An').closest('li')
  view.rerender(<DetailValues value={[second, first]} />)
  expect(screen.getByText('Trẻ An').closest('li')).toBe(original)
})
it('shows booleans and unknown values clearly and renders untrusted text as text', () => {
  render(
    <DetailValues
      value={{
        enabled: true,
        other: false,
        missing: null,
        count: 3,
        reason: '<img src=x onerror=alert(1)>',
      }}
    />,
  )
  expect(screen.getByText('Có')).toBeTruthy()
  expect(screen.getByText('Không')).toBeTruthy()
  expect(screen.getByText('Chưa có')).toBeTruthy()
  expect(screen.getByText('3')).toBeTruthy()
  expect(screen.getByText('<img src=x onerror=alert(1)>')).toBeTruthy()
  expect(document.querySelector('img')).toBeNull()
})
it('bounds recursive details and supports repeated primitive values', () => {
  const value: Record<string, unknown> = { values: ['Trùng', 'Trùng'] }
  value.self = value
  render(<DetailValues value={value} />)
  expect(screen.getAllByText('Trùng').length).toBeGreaterThanOrEqual(2)
  expect(
    screen.getAllByText(
      'Thông tin có nhiều cấp; xem từng phần trong hồ sơ nguồn.',
    ).length,
  ).toBeGreaterThan(0)
})
