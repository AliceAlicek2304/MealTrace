# MealTrace — Đặc tả Hợp đồng Photo API (Phase 2 / Baseline v1.4)

> **Mã task:** P2-N03  
> **Người bàn giao (Backend):** Nguyễn Duy Nam (BE2)  
> **Người tiếp nhận (Mobile/Android):** Nguyễn Văn Cường  
> **Phạm vi:** Bàn giao giao thức tải ảnh minh chứng món ăn (Meal Evidence), cơ chế idempotent khi mất mạng và kiểm soát quyền truy cập byte ảnh riêng tư cho phụ huynh.

---

## 1. Tổng quan các luồng Endpoint

| STT | Phương thức | Đường dẫn (Route) | Vai trò | Mục đích |
| --- | --- | --- | --- | --- |
| 1 | `POST` | `/api/kitchen/servings/{id}/photos` | `KITCHEN_STAFF` | Tải ảnh minh chứng phục vụ món ăn (multipart) |
| 2 | `GET` | `/api/kitchen/photos/{id}` | `KITCHEN_STAFF`, `ADMIN` | Xem chi tiết metadata của ảnh đã tải lên |
| 3 | `POST` | `/api/admin/photos/{id}/review` | `ADMIN` | Phê duyệt hoặc từ chối ảnh minh chứng |
| 4 | `GET` | `/api/parent/students/{studentId}/photos` | `PARENT` | Danh sách metadata ảnh đã duyệt của lớp con |
| 5 | `GET` | `/api/parent/students/{studentId}/photos/{photoId}/content` | `PARENT` | Tải luồng byte ảnh nhị phân bảo mật |

---

## 2. Chi tiết hợp đồng API

### 2.1. Upload ảnh phục vụ (Bếp thực hiện)
* **Endpoint:** `POST /api/kitchen/servings/{servingId}/photos`
* **Content-Type:** `multipart/form-data`
* **Các trường Form:**
  1. `file`: File ảnh nhị phân (Định dạng hỗ trợ: `image/jpeg`, `image/png`; dung lượng tối đa $\le$ 10 MiB).
  2. `metadata` (JSON String):
     ```json
     {
       "clientPhotoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
       "servingVersionId": "4a73b189-9d7a-4284-850f-2b36551b9e01",
       "capturedAt": "2026-11-19T11:05:00+07:00",
       "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
       "contentType": "image/jpeg"
     }
     ```
* **Cơ chế Idempotent (Chống gửi trùng khi mạng chập chờn / retry từ hàng đợi offline):**
  * **201 Created**: Thành công lần đầu. Trả về:
    ```json
    {
      "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
      "clientPhotoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "servingVersionId": "4a73b189-9d7a-4284-850f-2b36551b9e01",
      "state": "synced",
      "capturedAt": "2026-11-19T11:05:00+07:00",
      "receivedAt": "2026-11-19T11:06:12Z"
     }
    ```
  * **200 OK**: Khi app Android gửi lại (retry) với **CÙNG `clientPhotoId` VÀ CÙNG `sha256`**. Server không lưu lại file mới mà trả về chính bản ghi ảnh đã lưu trước đó.
  * **409 Conflict**: Gửi cùng `clientPhotoId` nhưng khác `sha256` (phát hiện mâu thuẫn dữ liệu từ thiết bị).
  * **400 / 422**: File không hợp lệ (sai signature header ảnh hoặc vượt quá 10 MiB).
  * **403 Forbidden**: Tài khoản không có vai trò `KITCHEN_STAFF`.

---

### 2.2. Xem thông tin chi tiết ảnh
* **Endpoint:** `GET /api/kitchen/photos/{id}`
* **Quyền:** `KITCHEN_STAFF`, `ADMIN`
* **Response (200 OK):**
  ```json
  {
    "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
    "servingVersionId": "4a73b189-9d7a-4284-850f-2b36551b9e01",
    "capturedAt": "2026-11-19T11:05:00+07:00",
    "receivedAt": "2026-11-19T11:06:12Z",
    "state": "pending",
    "reviewReason": null,
    "reviewedAt": null
  }
  ```

---

### 2.3. Phê duyệt ảnh minh chứng (Admin thực hiện)
* **Endpoint:** `POST /api/admin/photos/{id}/review`
* **Quyền:** `ADMIN`
* **Request Body (JSON):**
  ```json
  {
    "decision": "approve",
    "expectedVersion": 1,
    "reason": "Ảnh món ăn rõ ràng, đúng suất phục vụ."
  }
  ```
  *(Nếu `decision` là `"reject"` thì bắt buộc phải có `reason`).*
* **Response:**
  * **200 OK**: Cập nhật trạng thái thành công (`state = "approved"` hoặc `"rejected"`).
  * **409 Conflict**: Xung đột phiên bản kiểm tra (`expectedVersion` không khớp).

---

### 2.4. Phụ huynh xem danh sách ảnh đã duyệt
* **Endpoint:** `GET /api/parent/students/{studentId}/photos?from=2026-11-01&to=2026-11-30`
* **Quyền:** `PARENT`
* **Quy tắc bảo mật:**
  * Phụ huynh bắt buộc phải có liên kết hợp lệ (`ParentStudent` active) với `studentId`.
  * Chỉ trả về các ảnh có trạng thái `"approved"`. Ảnh `"pending"` hoặc `"rejected"` hoàn toàn không lộ ra client phụ huynh.
* **Response (200 OK):**
  ```json
  {
    "items": [
      {
        "id": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
        "mealDate": "2026-11-19",
        "mealType": "Lunch",
        "capturedAt": "2026-11-19T11:05:00+07:00",
        "contentUrl": "/api/parent/students/5c1a0172-23c8-4775-9c98-1e43c5b81a29/photos/9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d/content"
      }
    ]
  }
  ```

---

### 2.5. Tải luồng byte ảnh nhị phân (Private Guardian Bytes)
* **Endpoint:** `GET /api/parent/students/{studentId}/photos/{photoId}/content`
* **Quyền:** `PARENT`
* **Quy tắc bảo vệ dữ liệu:**
  * Header bắt buộc từ Server: `Cache-Control: no-store, private` (ngăn cấm proxy và cache công cộng lưu trữ).
  * Trả về luồng byte trực tiếp `image/jpeg` hoặc `image/png`.
  * Trả về **403 Forbidden** nếu phụ huynh không có liên kết với trẻ hoặc ảnh chưa được Admin duyệt. Không dùng URL public AWS/Cloudflare trực tiếp để tránh rò rỉ hình ảnh trẻ nhỏ.
