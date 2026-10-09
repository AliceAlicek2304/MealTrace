// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { MealDaysPage } from './MealDaysPage'

let cache: QueryClient
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
describe('Meal-day search UI', () => {
  it('uses the paginated search endpoint and sends only the final typed term', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], total: 0 } })
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={cache}><MealDaysPage /></QueryClientProvider>)
    await waitFor(() => expect(get).toHaveBeenCalledTimes(1))
    const search = screen.getByPlaceholderText('Bữa ăn hoặc tên món')
    fireEvent.change(search, { target: { value: 'B' } })
    fireEvent.change(search, { target: { value: 'Bữa' } })
    fireEvent.change(search, { target: { value: 'Bữa trưa' } })
    expect(screen.getByRole('status').textContent).toBe('Đang nhập tìm kiếm…')
    expect(get).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(get).toHaveBeenCalledTimes(2))
    expect(get).toHaveBeenLastCalledWith('/meal-days/search', { params: { page: 1, date: undefined, status: undefined, search: 'Bữa trưa' } })
  })
})
