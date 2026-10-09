import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../../lib/api'
import { listUsers } from './authApi'

afterEach(() => vi.restoreAllMocks())

describe('Account list API filters', () => {
  it('sends search, role and class with the requested page and preserves the server total', async () => {
    const response = { items: [], total: 107, page: 3, pageSize: 25 }
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: response })
    expect(await listUsers(3, 'class-id', '  Needle  ', 'TEACHER')).toEqual(response)
    expect(get).toHaveBeenCalledWith('/admin/users', { params: {
      page: 3, pageSize: 25, classId: 'class-id', search: 'Needle', role: 'TEACHER',
    } })
  })

  it('omits optional filters when viewing all accounts', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], total: 0, page: 1, pageSize: 25 } })
    await listUsers(1, '', ' ', 'ALL')
    expect(get).toHaveBeenCalledWith('/admin/users', { params: {
      page: 1, pageSize: 25, classId: undefined, search: undefined, role: undefined,
    } })
  })
})
