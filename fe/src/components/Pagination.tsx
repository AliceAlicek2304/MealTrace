export function Pagination({ page, total, pageSize, onChange, busy = false }: Readonly<{
  page: number; total: number; pageSize: number; onChange: (page: number) => void; busy?: boolean
}>) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  return <div className="pagination"><button type="button" className="button secondary" disabled={busy || page <= 1} onClick={() => onChange(page - 1)}>Trước</button>
    <span>Trang {page}/{pages} · {total} mục</span><button type="button" className="button secondary" disabled={busy || page >= pages} onClick={() => onChange(page + 1)}>Sau</button></div>
}
