// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { useState } from 'react'
import { afterEach, describe, expect, it } from 'vitest'
import { FilterPanel } from './FilterPanel'
import { SectionSwitcher } from './SectionSwitcher'

afterEach(cleanup)
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
