import { useId, useState, type ReactNode } from 'react'

export function RecordActions({ label, children }: Readonly<{ label: string; children: (close: () => void) => ReactNode }>) {
  const [open, setOpen] = useState(false)
  const id = useId()
  const close = () => setOpen(false)
  return <div className="record-actions">
    <button type="button" className="button secondary record-actions-toggle" aria-label={`Thao tác: ${label}`} aria-expanded={open} aria-controls={id} onClick={() => setOpen(value => !value)}>
      Thao tác <span aria-hidden="true">{open ? '−' : '+'}</span>
    </button>
    <div id={id} className={`table-actions record-actions-content ${open ? 'record-actions-open' : ''}`}>{children(close)}</div>
  </div>
}
