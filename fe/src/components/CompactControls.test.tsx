// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { QueryState } from './QueryState'
import { useState } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { FilterPanel } from './FilterPanel'
import { SectionSwitcher } from './SectionSwitcher'

afterEach(cleanup)

it('announces query failures and lets the user retry before returning to content', () => {
  const retry = vi.fn()
  const view = render(<QueryState loading={false} error loadingMessage="Đang tải…" errorMessage="Không tải được danh sách." onRetry={retry}><p>Danh sách trẻ</p></QueryState>)
  expect(screen.getByRole('alert').textContent).toContain('Không tải được danh sách.')
  fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }))
  expect(retry).toHaveBeenCalledOnce()
  view.rerender(<QueryState loading error={false} loadingMessage="Đang tải…" errorMessage=""><p>Danh sách trẻ</p></QueryState>)
  expect(screen.getByRole('status').textContent).toBe('Đang tải…')
  view.rerender(<QueryState loading={false} error={false} loadingMessage="" errorMessage=""><p>Danh sách trẻ</p></QueryState>)
  expect(screen.getByText('Danh sách trẻ')).toBeTruthy()
  expect(screen.queryByRole('alert')).toBeNull()
})
describe('Compact controls', () => {
  it('keeps entered filters when closing and reopening and reports active conditions', () => {
    render(<FilterPanel activeCount={1}><input aria-label="Tên trẻ" defaultValue="An" /></FilterPanel>)
    const button = screen.getByRole('button', { name: /Tìm kiếm & bộ lọc/ })
    expect(button.getAttribute('aria-expanded')).toBe('false')
    expect(button.textContent).toContain('1 đang dùng')
    fireEvent.click(button)
    fireEvent.change(screen.getByLabelText('Tên trẻ'), { target: { value: 'Bình' } })
    fireEvent.click(button)
    fireEvent.click(button)
    expect((screen.getByLabelText('Tên trẻ') as HTMLInputElement).value).toBe('Bình')
    expect(button.getAttribute('aria-expanded')).toBe('true')
  })
  it('keeps the dropdown and desktop buttons on the same selected section', () => {
    function Example() {
      const [value, setValue] = useState('current')
      return <SectionSwitcher label="Nội dung" value={value} onChange={setValue} options={[{ value: 'current', label: 'Hiện hành' }, { value: 'history', label: 'Lịch sử' }]} />
    }
    render(<Example />)
    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'history' } })
    expect(screen.getByRole('button', { name: 'Lịch sử' }).getAttribute('aria-pressed')).toBe('true')
    fireEvent.click(screen.getByRole('button', { name: 'Hiện hành' }))
    expect((screen.getByRole('combobox') as HTMLSelectElement).value).toBe('current')
  })
})
