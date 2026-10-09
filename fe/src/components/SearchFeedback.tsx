export function SearchFeedback({ waiting, fetching }: { waiting: boolean; fetching: boolean }) {
  return waiting || fetching ? <output className="muted compact" aria-live="polite">{waiting ? 'Đang nhập tìm kiếm…' : 'Đang cập nhật kết quả…'}</output> : null
}
