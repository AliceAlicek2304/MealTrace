// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { StudentDirectory } from './StudentDirectory'

let cache: QueryClient
afterEach(() => { cleanup(); cache.clear(); vi.restoreAllMocks() })
describe('Class directory search', () => {
  it('debounces class search, uses the API search parameter and distinguishes an empty list from no matches', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], total: 0 } })
    cache = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(<QueryClientProvider client={cache}><StudentDirectory view="classes" initialClassId="" onLink={vi.fn()} onCreate={vi.fn()} onViewStudents={vi.fn()} /></QueryClientProvider>)
    expect(await screen.findByText('Chưa có lớp.')).toBeTruthy()
    fireEvent.change(screen.getByPlaceholderText('Tên lớp hoặc niên khóa'), { target: { value: 'M1' } })
    await waitFor(() => expect(get).toHaveBeenLastCalledWith('/admin/classes', { params: { search: 'M1', page: 1 } }))
    expect(await screen.findByText('Không có lớp phù hợp tìm kiếm.')).toBeTruthy()
  })
})
