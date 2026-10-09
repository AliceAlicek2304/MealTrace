export function SearchFeedback({ waiting, fetching }: { waiting: boolean; fetching: boolean }) {
  return waiting || fetching ? <p className="muted compact" role="status">{waiting ? 'Đang nhập tìm kiếm…' : 'Đang cập nhật kết quả…'}</p> : null
}
