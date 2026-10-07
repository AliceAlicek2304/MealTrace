import { useLayoutEffect, useRef, useState, type TableHTMLAttributes } from 'react'

const secondaryHeadings = new Set(['Năm học', 'Thời điểm lưu', 'Người gửi / bản nguồn', 'Phạm vi', 'Phiên đã tạo', 'Ghi chú'])

/** One set of data/actions: desktop table, labelled record cards on small screens. */
export function ResponsiveTable({ className = '', children, ...props }: TableHTMLAttributes<HTMLTableElement>) {
  const ref = useRef<HTMLTableElement>(null)
  const [expanded, setExpanded] = useState(false)
  const [hasSecondary, setHasSecondary] = useState(false)
  useLayoutEffect(() => {
    const table = ref.current!
    table.tHead?.setAttribute('role', 'rowgroup')
    for (const row of table.tHead?.rows ?? []) {
      row.setAttribute('role', 'row')
      for (const cell of row.cells) cell.setAttribute('role', 'columnheader')
    }
    const headings = Array.from(table.tHead?.rows[0]?.cells ?? []).map(cell => cell.textContent?.trim() || 'Thao tác')
    setHasSecondary(headings.some(heading => secondaryHeadings.has(heading)) && Array.from(table.tBodies).some(body => Array.from(body.rows).some(row => row.cells.length > 1)))
    for (const body of table.tBodies) {
      body.setAttribute('role', 'rowgroup')
      for (const row of body.rows) {
        let column = 0
        row.setAttribute('role', 'row')
        for (const cell of row.cells) {
          cell.setAttribute('role', 'cell')
          cell.dataset.label = headings[column] ?? 'Thông tin'
          cell.dataset.secondary = String(secondaryHeadings.has(headings[column]))
          cell.dataset.fullRow = String(cell.colSpan > 1)
          column += cell.colSpan
        }
      }
    }
  })
  return <>{hasSecondary && <button type="button" className="record-details-toggle" aria-expanded={expanded} onClick={() => setExpanded(value => !value)}>{expanded ? 'Ẩn thông tin bổ sung' : 'Hiện thông tin bổ sung'}</button>}
    <table {...props} ref={ref} role="table" className={`responsive-table ${expanded ? 'records-expanded' : ''} ${className}`}>{children}</table></>
}
