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
function scalarText(value: unknown): string {
  if (typeof value === 'boolean') return value ? 'Có' : 'Không'
  if (typeof value === 'string') {
    if (/^\d{4}-\d{2}-\d{2}T/.test(value)) return schoolDateTime(value)
    return value
  }
  if (typeof value === 'number' || typeof value === 'bigint')
    return value.toString()
  return 'Dữ liệu không hỗ trợ'
}

function valueIdentity(value: unknown): string {
  if (value !== null && typeof value === 'object' && 'id' in value) {
    if (typeof value.id === 'string' || typeof value.id === 'number')
      return `id:${value.id}`
  }
  try {
    return JSON.stringify(value) ?? scalarText(value)
  } catch {
    return scalarText(value)
  }
}

function keyedValues(values: readonly unknown[]) {
  const occurrences = new Map<string, number>()
  return values.map((value) => {
    const identity = valueIdentity(value)
    const occurrence = occurrences.get(identity) ?? 0
    occurrences.set(identity, occurrence + 1)
    return { value, key: `${identity}:${occurrence}` }
  })
}

export function DetailValues({
  value,
  depth = 0,
}: Readonly<{ value: unknown; depth?: number }>) {
  if (depth > 5)
    return <p>Thông tin có nhiều cấp; xem từng phần trong hồ sơ nguồn.</p>
  if (value === null || value === undefined) return <span>Chưa có</span>
  if (Array.isArray(value))
    return (
      <ol>
        {keyedValues(value).map((item) => (
          <li key={item.key}>
            <DetailValues value={item.value} depth={depth + 1} />
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
  return <span style={{ overflowWrap: 'anywhere' }}>{scalarText(value)}</span>
}
