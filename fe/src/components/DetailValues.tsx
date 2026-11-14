import { schoolDateTime } from '../lib/schoolTime'

const labels: Record<string, string> = {
  fullName: 'Họ tên',
  phoneNumber: 'Số điện thoại',
  email: 'Email',
  roles: 'Vai trò',
  status: 'Trạng thái',
  classIds: 'Lớp được phân công',
  studentIds: 'Trẻ được liên kết',
  inspectorAccessUntil: 'Quyền xem đến',
  isActive: 'Đang hoạt động',
  mealDayId: 'Phiên ăn',
  settlementId: 'Bản chốt',
  signedAt: 'Thời điểm ký',
  energyKcalPerPortion: 'Năng lượng mỗi suất',
  costPerPortion: 'Chi phí mỗi suất',
  perServing: 'Dinh dưỡng mỗi suất',
  lines: 'Thành phần',
  inputs: 'Dữ liệu tính',
  roundingRule: 'Quy tắc làm tròn',
  incompleteData: 'Dữ liệu còn thiếu',
  pinnedPerServing: 'Theo bản đã lưu',
  deltaFromPinned: 'Chênh lệch so với bản đã lưu',
  mode: 'Cách tính',
  id: 'Mã',
  source: 'Nguồn',
  occurredAt: 'Thời điểm',
  actor: 'Người thực hiện',
  action: 'Thao tác',
  entityType: 'Loại hồ sơ',
  entityId: 'Mã hồ sơ',
  reason: 'Lý do',
  before: 'Trước thay đổi',
  after: 'Sau thay đổi',
  name: 'Tên',
  correlationId: 'Mã đối chiếu',
  sourceJson: 'Nguồn dữ liệu',
  version: 'Phiên bản',
  createdAt: 'Ngày tạo',
}
export function DetailValues({
  value,
  depth = 0,
}: {
  value: unknown
  depth?: number
}) {
  if (depth > 5)
    return <p>Thông tin có nhiều cấp; xem từng phần trong hồ sơ nguồn.</p>
  if (value === null || value === undefined) return <span>Chưa có</span>
  if (Array.isArray(value))
    return (
      <ol>
        {value.map((x, i) => (
          <li key={i}>
            <DetailValues value={x} depth={depth + 1} />
          </li>
        ))}
      </ol>
    )
  if (typeof value === 'object')
    return (
      <dl className="record-detail-values">
        {Object.entries(value).map(([key, v]) => (
          <div key={key}>
            <dt>{labels[key] ?? key}</dt>
            <dd>
              <DetailValues value={v} depth={depth + 1} />
            </dd>
          </div>
        ))}
      </dl>
    )
  const text = String(value)
  const display = /^\d{4}-\d{2}-\d{2}T/.test(text)
    ? schoolDateTime(text)
    : typeof value === 'boolean'
      ? value
        ? 'Có'
        : 'Không'
      : text
  return <span style={{ overflowWrap: 'anywhere' }}>{display}</span>
}
