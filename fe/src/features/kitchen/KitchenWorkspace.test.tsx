// @vitest-environment jsdom
import {
  cleanup,
  fireEvent,
  render,
  screen,
  within,
} from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { KitchenWorkspace } from './KitchenWorkspace'
vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
  Toaster: () => null,
}))
afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})
it('submits all required nutritional fields for the selected ingredient', async () => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.setAttribute('open', '')
    },
  })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.removeAttribute('open')
    },
  })
  vi.spyOn(api, 'get').mockResolvedValue({
    data: { items: [{ id: 'ingredient', name: 'Gạo', unit: 'g' }], total: 1 },
  })
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={cache}>
      <KitchenWorkspace />
    </QueryClientProvider>,
  )
  fireEvent.click(await screen.findByRole('button', { name: 'Dinh dưỡng' }))
  const dialog = within(screen.getByRole('dialog'))
  for (const [label, value] of [
    ['Năng lượng / 100g (kcal)', '360'],
    ['Đạm / 100g (g)', '7'],
    ['Béo / 100g (g)', '1'],
    ['Carbohydrate / 100g (g)', '70'],
    ['Nguồn dữ liệu', 'Bảng thành phần thử nghiệm'],
  ])
    fireEvent.change(dialog.getByLabelText(label), { target: { value } })
  fireEvent.submit(dialog.getByRole('button', { name: 'Lưu' }).closest('form')!)
  await vi.waitFor(() =>
    expect(post).toHaveBeenCalledWith(
      '/kitchen/ingredients/ingredient/ingredient-versions',
      expect.objectContaining({
        proteinG: 7,
        fatG: 1,
        carbG: 70,
        source: 'Bảng thành phần thử nghiệm',
      }),
    ),
  )
  cache.clear()
})

it('submits total batch quantities and preserves the form after a recipe version conflict', async () => {
  Object.defineProperty(HTMLDialogElement.prototype, 'showModal', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.setAttribute('open', '')
    },
  })
  Object.defineProperty(HTMLDialogElement.prototype, 'close', {
    configurable: true,
    value: function (this: HTMLDialogElement) {
      this.removeAttribute('open')
    },
  })
  vi.spyOn(api, 'get').mockResolvedValue({
    data: { items: [{ id: 'ingredient', name: 'Gạo', unit: 'g' }], total: 1 },
  })
  const post = vi.spyOn(api, 'post').mockRejectedValue(new Error('conflict'))
  const cache = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  })
  render(
    <QueryClientProvider client={cache}>
      <KitchenWorkspace />
    </QueryClientProvider>,
  )
  fireEvent.click(screen.getByRole('button', { name: 'Tạo / thêm phiên bản' }))
  const modal = within(screen.getByRole('dialog'))
  fireEvent.change(
    modal.getByRole('combobox', { name: 'Thao tác công thức' }),
    { target: { value: 'version' } },
  )
  fireEvent.change(modal.getByLabelText('Mã công thức'), {
    target: { value: 'recipe-id' },
  })
  fireEvent.change(modal.getByLabelText('Phiên bản đối chiếu'), {
    target: { value: '2' },
  })
  fireEvent.change(modal.getByLabelText('Số suất'), { target: { value: '30' } })
  await modal.findByRole('option', { name: 'Gạo (g)' })
  fireEvent.change(modal.getByRole('combobox', { name: 'Thêm thành phần' }), {
    target: { value: 'ingredient' },
  })
  fireEvent.change(modal.getByLabelText(/Gạo.*tổng lượng/), {
    target: { value: '3000.5' },
  })
  fireEvent.submit(modal.getByRole('button', { name: 'Lưu' }).closest('form')!)
  await vi.waitFor(() =>
    expect(post).toHaveBeenCalledWith(
      '/kitchen/recipes/recipe-id/versions',
      expect.objectContaining({
        expectedVersion: 2,
        servingBasis: 30,
        lines: [{ ingredientId: 'ingredient', unit: 'g', quantity: 3000.5 }],
      }),
    ),
  )
  await vi.waitFor(() =>
    expect(
      modal.getByRole('button', { name: 'Lưu' }).hasAttribute('disabled'),
    ).toBe(false),
  )
  expect(post).toHaveBeenCalledTimes(1)
  expect((modal.getByLabelText('Mã công thức') as HTMLInputElement).value).toBe(
    'recipe-id',
  )
  cache.clear()
})
