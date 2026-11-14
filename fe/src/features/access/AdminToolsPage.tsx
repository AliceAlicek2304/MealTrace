import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { toast } from 'sonner'
import { api, apiErrorMessage } from '../../lib/api'
import { Modal } from '../../components/Modal'
import { Pagination } from '../../components/Pagination'
import { ResponsiveTable } from '../../components/ResponsiveTable'
import { QueryState } from '../../components/QueryState'
import { FilterPanel } from '../../components/FilterPanel'
import { DetailValues } from '../../components/DetailValues'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { schoolDateTime } from '../../lib/schoolTime'

type Entry = {
  id: string
  occurredAt: string
  actor?: { name: string }
  action: string
  entityType: string
  entityId: string
  reason?: string
}
function notificationStatus(
  pending: boolean,
  error: boolean,
  enabled: boolean,
) {
  if (pending) return 'Đang tải trạng thái gửi tin…'
  if (error) return 'Không tải được trạng thái gửi tin.'
  if (enabled) return 'Dịch vụ gửi tin đang bật.'
  return 'Dịch vụ gửi tin chưa bật.'
}
export function AdminToolsPage() {
  const [page, setPage] = useState(1),
    [entityType, setEntityType] = useState(''),
    [entityId, setEntityId] = useState(''),
    [action, setAction] = useState('')
  const [detail, setDetail] = useState<unknown>(null),
    [report, setReport] = useState(''),
    [busy, setBusy] = useState(false),
    [confirm, setConfirm] = useState(false)
  const filter = useDebouncedValue(
    useMemo(
      () => ({ entityType, entityId, action }),
      [entityType, entityId, action],
    ),
  )
  const params = {
    entityType: filter.entityType.trim() || undefined,
    entityId: filter.entityId.trim() || undefined,
    action: filter.action.trim() || undefined,
    page,
    pageSize: 25,
  }
  const path =
    params.entityType && params.entityId
      ? `/admin/audit-logs/entities/${encodeURIComponent(params.entityType)}/${encodeURIComponent(params.entityId)}`
      : '/admin/audit-logs'
  const logs = useQuery({
    queryKey: ['audit', params],
    queryFn: async () =>
      (await api.get<{ items: Entry[]; total: number }>(path, { params })).data,
  })
  const settings = useQuery({
    queryKey: ['notification-settings'],
    queryFn: async () => (await api.get('/notifications/settings')).data,
  })
  async function load(path: string) {
    setBusy(true)
    try {
      setDetail((await api.get(path)).data)
    } catch (error) {
      toast.error(apiErrorMessage(error))
    } finally {
      setBusy(false)
    }
  }
  async function exportCsv() {
    setBusy(true)
    try {
      const response = await api.get('/admin/audit-logs/export', {
        params: { ...params, format: 'csv' },
        responseType: 'blob',
      })
      const url = URL.createObjectURL(response.data)
      const link = document.createElement('a')
      link.href = url
      link.download = 'lich-su-xu-ly.csv'
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      toast.error(apiErrorMessage(error))
    } finally {
      setBusy(false)
    }
  }
  async function send() {
    setBusy(true)
    try {
      await api.post('/admin/notifications/test')
      setConfirm(false)
      toast.success('Đã gửi yêu cầu gửi tin thử.')
    } catch (error) {
      toast.error(apiErrorMessage(error), { toasterId: 'edit-modal' })
    } finally {
      setBusy(false)
    }
  }
  return (
    <>
      <h1>Lịch sử xử lý & tra cứu</h1>
      <section className="panel workflow-lists">
        <div className="panel-head">
          <h2>Lịch sử xử lý</h2>
          <button
            type="button"
            className="button secondary"
            disabled={busy}
            onClick={() => {
              void exportCsv()
            }}
          >
            Xuất CSV
          </button>
        </div>
        <FilterPanel
          activeCount={[entityType, entityId, action].filter(Boolean).length}
        >
          <form
            className="workflow-form"
            onSubmit={(e) => {
              e.preventDefault()
              setPage(1)
              void logs.refetch()
            }}
          >
            <label className="field">
              <span>Loại hồ sơ</span>
              <input
                value={entityType}
                onChange={(e) => {
                  setEntityType(e.target.value)
                  setPage(1)
                }}
              />
            </label>
            <label className="field">
              <span>Mã hồ sơ</span>
              <input
                value={entityId}
                onChange={(e) => {
                  setEntityId(e.target.value)
                  setPage(1)
                }}
              />
            </label>
            <label className="field">
              <span>Thao tác</span>
              <input
                value={action}
                onChange={(e) => {
                  setAction(e.target.value)
                  setPage(1)
                }}
              />
            </label>
            <button type="submit" className="button secondary">
              Tải lại
            </button>
          </form>
        </FilterPanel>
        <QueryState
          loading={logs.isPending}
          error={logs.isError}
          loadingMessage="Đang tải…"
          errorMessage="Không tải được lịch sử."
        >
          <div className="table-wrap">
            <ResponsiveTable>
              <thead>
                <tr>
                  <th>Thời điểm</th>
                  <th>Người thực hiện</th>
                  <th>Thao tác</th>
                  <th>Hồ sơ</th>
                  <th>Chi tiết</th>
                </tr>
              </thead>
              <tbody>
                {logs.data?.items.map((x) => (
                  <tr key={x.id}>
                    <td>{schoolDateTime(x.occurredAt)}</td>
                    <td>{x.actor?.name ?? 'Hệ thống'}</td>
                    <td>{x.action}</td>
                    <td>{x.entityType}</td>
                    <td>
                      <button
                        type="button"
                        className="button secondary"
                        disabled={busy}
                        onClick={() => {
                          void load(
                            `/admin/audit-logs/${encodeURIComponent(x.id)}`,
                          )
                        }}
                      >
                        Xem
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </ResponsiveTable>
            {logs.data?.items.length === 0 && (
              <p className="empty-state">Không có lịch sử phù hợp.</p>
            )}
          </div>
        </QueryState>
        <Pagination
          page={page}
          total={logs.data?.total ?? 0}
          pageSize={25}
          onChange={setPage}
        />
      </section>
      <section className="panel">
        <form
          className="workflow-form"
          onSubmit={(e) => {
            e.preventDefault()
            void load(`/reports/${encodeURIComponent(report)}/lineage`)
          }}
        >
          <h2>Truy vết báo cáo</h2>
          <label className="field">
            <span>Mã báo cáo</span>
            <input
              required
              value={report}
              onChange={(e) => setReport(e.target.value)}
            />
          </label>
          <button className="button secondary" type="submit" disabled={busy}>
            Xem nguồn báo cáo
          </button>
        </form>
      </section>
      <section className="panel workflow-form">
        <h2>Kiểm tra gửi thông báo</h2>
        <p>
          {notificationStatus(
            settings.isPending,
            settings.isError,
            !!settings.data?.enabled,
          )}
        </p>
        <button
          type="button"
          className="button secondary"
          disabled={busy || !settings.data?.enabled}
          onClick={() => setConfirm(true)}
        >
          Gửi tin thử
        </button>
      </section>
      {detail !== null && (
        <Modal title="Chi tiết hồ sơ" onClose={() => setDetail(null)}>
          <DetailValues value={detail} />
        </Modal>
      )}
      {confirm && (
        <Modal
          title="Xác nhận gửi tin thử"
          busy={busy}
          onClose={() => setConfirm(false)}
        >
          <p>Gửi một tin nhắn đến số nhận thử đã được cấu hình?</p>
          <div className="form-actions">
            <button
              type="button"
              className="button secondary"
              disabled={busy}
              onClick={() => setConfirm(false)}
            >
              Quay lại
            </button>
            <button
              type="button"
              className="button primary"
              disabled={busy}
              onClick={() => {
                void send()
              }}
            >
              Xác nhận gửi
            </button>
          </div>
        </Modal>
      )}
    </>
  )
}
