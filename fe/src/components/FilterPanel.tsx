import { useId, useState, type ReactNode } from 'react'

export function FilterPanel({ children, activeCount = 0 }: Readonly<{ children: ReactNode; activeCount?: number }>) {
  const [open, setOpen] = useState(false)
  const id = useId()
  return <div className="filter-panel">
    <button type="button" className="filter-toggle" aria-expanded={open} aria-controls={id} onClick={() => setOpen(value => !value)}>
      Tìm kiếm & bộ lọc{activeCount > 0 && <span>{activeCount} đang dùng</span>}<span aria-hidden="true">{open ? '−' : '+'}</span>
    </button>
    <div id={id} className={`filter-content ${open ? 'filter-content-open' : ''}`}>{children}</div>
  </div>
}
