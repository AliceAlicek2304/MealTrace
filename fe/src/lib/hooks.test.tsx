// @vitest-environment jsdom
import { act, cleanup, renderHook } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useDeadline } from './useDeadline'
import { useDebouncedValue } from './useDebouncedValue'

afterEach(() => { cleanup(); vi.useRealTimers() })

describe('Search and cutoff updates', () => {
  it('settles only the latest search after typing stops and clears timers on unmount', () => {
    vi.useFakeTimers()
    const { result, rerender, unmount } = renderHook(({ value }) => useDebouncedValue(value), { initialProps: { value: '' } })
    rerender({ value: 'a' })
    act(() => vi.advanceTimersByTime(200))
    rerender({ value: 'abc' })
    act(() => vi.advanceTimersByTime(299))
    expect(result.current).toBe('')
    act(() => vi.advanceTimersByTime(1))
    expect(result.current).toBe('abc')
    rerender({ value: 'next' })
    unmount()
    expect(vi.getTimerCount()).toBe(0)
  })
  it('updates exactly at cutoff without user interaction and resets for another session', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-10-06T00:29:59Z'))
    const { result, rerender, unmount } = renderHook(({ value }) => useDeadline(value), { initialProps: { value: '2026-10-06T00:30:00Z' } })
    expect(result.current).toBe(false)
    act(() => vi.advanceTimersByTime(1000))
    expect(result.current).toBe(true)
    rerender({ value: '2026-10-07T00:30:00Z' })
    expect(result.current).toBe(false)
    act(() => {
      vi.setSystemTime(new Date('2026-10-07T00:31:00Z'))
      window.dispatchEvent(new Event('focus'))
    })
    expect(result.current).toBe(true)
    unmount()
    expect(vi.getTimerCount()).toBe(0)
  })
})
