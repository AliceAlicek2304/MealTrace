// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { ResponsiveTable } from './ResponsiveTable'

afterEach(cleanup)

describe('ResponsiveTable', () => {
  it('keeps each value associated with its heading and preserves row actions', () => {
    const { rerender } = render(<ResponsiveTable><thead><tr><th>Ngày ăn</th><th /></tr></thead><tbody><tr><td>2026-10-09</td><td><button type="button">Xem phiên</button></td></tr></tbody></ResponsiveTable>)
    expect(screen.getByText('2026-10-09').dataset.label).toBe('Ngày ăn')
    expect(screen.getByRole('button', { name: 'Xem phiên' }).closest('td')?.dataset.label).toBe('Thao tác')
    rerender(<ResponsiveTable><thead><tr><th>Lý do</th><th /></tr></thead><tbody><tr><td>Nghỉ học</td><td><button type="button">Xem phiên</button></td></tr></tbody></ResponsiveTable>)
    expect(screen.getByText('Nghỉ học').dataset.label).toBe('Lý do')
  })
  it('shows empty and loading messages once without a misleading column label', () => {
    render(<ResponsiveTable><thead><tr><th>Ngày ăn</th><th>Bữa ăn</th></tr></thead><tbody><tr><td colSpan={2}>Chưa có phiên ăn</td></tr></tbody></ResponsiveTable>)
    expect(screen.getByText('Chưa có phiên ăn').dataset.fullRow).toBe('true')
  })
  it('lets the user reveal supplemental fields without losing the record or actions', () => {
    render(<ResponsiveTable><thead><tr><th>Lớp</th><th>Năm học</th></tr></thead><tbody><tr><td>A1</td><td>2026-2027</td></tr></tbody></ResponsiveTable>)
    fireEvent.click(screen.getByRole('button', { name: 'Hiện thông tin bổ sung' }))
    expect(screen.getByRole('button', { name: 'Ẩn thông tin bổ sung' }).getAttribute('aria-expanded')).toBe('true')
    expect(screen.getByText('A1')).toBeTruthy()
    expect(screen.getByText('2026-2027')).toBeTruthy()
  })
  it('keeps the identifying first column primary even when it normally contains supplemental information', () => {
    render(<ResponsiveTable><thead><tr><th>Năm học</th><th>Giờ chốt</th><th>Thao tác</th></tr></thead><tbody><tr><td>2026-2027</td><td>07:00</td><td><button type="button">Xem lịch</button></td></tr></tbody></ResponsiveTable>)
    expect(screen.getByText('2026-2027').dataset.secondary).toBe('false')
    expect(screen.getByText('07:00').dataset.secondary).toBe('true')
    fireEvent.click(screen.getByRole('button', { name: 'Hiện thông tin bổ sung' }))
    expect(screen.getByRole('table').classList.contains('records-expanded')).toBe(true)
    expect(screen.getByRole('button', { name: 'Xem lịch' })).toBeTruthy()
  })
  it('can reveal a long student code inside the name field without hiding the child name or action', () => {
    render(<ResponsiveTable><thead><tr><th>Trẻ</th><th>Thao tác</th></tr></thead><tbody><tr><td><strong>Nguyễn An</strong><small className="record-secondary">HS-1234567890ABCDEF</small></td><td><button type="button">Xem hồ sơ</button></td></tr></tbody></ResponsiveTable>)
    fireEvent.click(screen.getByRole('button', { name: 'Hiện thông tin bổ sung' }))
    expect(screen.getByRole('table').classList.contains('records-expanded')).toBe(true)
    expect(screen.getByText('Nguyễn An')).toBeTruthy()
    expect(screen.getByText('HS-1234567890ABCDEF')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Xem hồ sơ' })).toBeTruthy()
  })
})
