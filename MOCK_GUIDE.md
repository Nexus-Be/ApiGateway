# HƯỚNG DẪN TẠO MOCK DATA TẠI API GATEWAY DÀNH CHO DEVELOPER
## (QUY TRÌNH MOCK DỮ LIỆU ĐỘC LẬP CHO FRONTEND & BACKEND TEAM)

> **Mục tiêu**: Hướng dẫn bất kỳ developer nào (Frontend, Backend, QA) đọc xong tài liệu này trong **3 phút** là có thể tự tay tạo được dữ liệu Mock cho một API bất kỳ để phát triển giao diện mà không cần chờ Backend code xong.

---

## 💡 Tổng Quan: Có 2 Cách Mock Dữ Liệu Tại Gateway

| Phương Pháp | Khi Nào Sử Dụng? | Cần Viết Code C# Không? | Có Hiện Trên Swagger Không? |
| :--- | :--- | :---: | :---: |
| **Cách 1: Declarative Mock (`*.mock.json`)**<br>*(Khuyên dùng cho FE)* | Khi Backend **chưa có code C#**, FE cần ngay 1 endpoint REST để ghép giao diện. | ❌ **0 dòng code C#** | ✅ **Tự động hiện lên Swagger** |
| **Cách 2: Controller Seed Fixtures (`Seeds/`)**<br>*(Khuyên dùng cho BE)* | Khi đã viết **Controller Action C#**, cần dữ liệu mẫu chuẩn Protobuf gRPC khi bật chế độ Mock. | ✅ Có Action C# | ✅ Hiện theo Controller Swagger |

---

# CÁCH 1: TẠO MOCK API SIÊU TỐC (0 DÒNG C# — ĂN NGAY TRONG 30 GIÂY)

Dành cho trường hợp: Bạn cần tạo một API mới tinh (ví dụ `GET /api/v1/catalog/promotions`) để Frontend gọi thử mà không cần đụng vào code C#.

### Bước 1: Tạo file JSON tại thư mục `Mocks/`
Vào thư mục: `Nexus.Gateway/Mocks/`  
Tạo một file có đuôi bắt buộc là: `*.mock.json` (hoặc `*.def.json`).  
*Ví dụ:* `Nexus.Gateway/Mocks/promotions.mock.json`

> 💡 **5 File Mẫu Chuẩn Có Sẵn Để Tham Khảo**:
> Trong thư mục `Nexus.Gateway/Mocks/` đã có sẵn 5 file mẫu chuẩn theo entity của `UserService` và `ApiResponseFactory`:
> - `auth-register.mock.json` (`POST /api/v1/auth/register`) — Đăng ký tài khoản (`PendingVerification`).
> - `auth-resend-otp.mock.json` (`POST /api/v1/auth/resend-otp`) — Gửi lại OTP (`OtpResponse`).
> - `auth-verify-otp.mock.json` (`POST /api/v1/auth/verify-otp`) — Kích hoạt tài khoản (`Active`).
> - `auth-login.mock.json` (`POST /api/v1/auth/login`) — Đăng nhập trả về JWT token dev (`LoginResponse`).
> - `users-me.mock.json` (`GET /api/v1/users/me`) — Lấy profile cá nhân với quyền `policy: "authenticated"`.

---

### Bước 2: Copy một trong 3 Template mẫu dưới đây

#### 👉 Template A: API Trả Về 1 Đối Tượng Đơn Lẻ (Single Object)
Gateway sẽ tự động bọc dữ liệu của bạn vào vỏ chuẩn: `{"success": true, "statusCode": 200, "data": { ... }}`

```json
{
  "id": "mock-get-promotion-detail",
  "service": "catalog",
  "method": "GET",
  "path": "/api/v1/catalog/promotions/{id}",
  "description": "Lấy chi tiết mã khuyến mãi",
  "policy": "public",
  "statusCode": 200,
  "responseType": "single",
  "message": "Lấy thông tin khuyến mãi thành công.",
  "response": {
    "id": "promo-001",
    "title": "Siêu sale công nghệ 50%",
    "discountPercent": 50,
    "maxDiscountAmount": "500000.00",
    "startDate": "2026-10-01T00:00:00Z",
    "endDate": "2026-10-31T23:59:59Z"
  }
}
```

---

#### 👉 Template B: API Trả Về Danh Sách Phân Trang (Paged List)
Gateway tự động hỗ trợ nhận Query String `?page=1&pageSize=20` và bọc dữ liệu phân trang:

```json
{
  "id": "mock-list-promotions-paged",
  "service": "catalog",
  "method": "GET",
  "path": "/api/v1/catalog/promotions",
  "description": "Danh sách khuyến mãi đang chạy",
  "policy": "public",
  "statusCode": 200,
  "responseType": "paged",
  "response": {
    "items": [
      {
        "id": "promo-001",
        "title": "Giảm 50K cho đơn từ 200K",
        "code": "TECH50K"
      },
      {
        "id": "promo-002",
        "title": "Miễn phí vận chuyển toàn quốc",
        "code": "FREESHIP"
      }
    ],
    "total": 2
  }
}
```

---

#### 👉 Template C: API Giả Lập Báo Lỗi (Để Frontend Test Xử Lý Lỗi UI)
Khi bạn muốn test xem giao diện xử lý ra sao khi API trả về lỗi 400 hoặc 404:

```json
{
  "id": "mock-promotion-out-of-stock-error",
  "service": "catalog",
  "method": "POST",
  "path": "/api/v1/catalog/promotions/apply",
  "description": "Giả lập lỗi voucher đã hết lượt",
  "policy": "public",
  "statusCode": 400,
  "responseType": "raw",
  "response": {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Voucher Expired",
    "status": 400,
    "errorCode": "VOUCHER_DEPLETED",
    "detail": "Mã khuyến mãi này đã hết lượt sử dụng."
  }
}
```

---

### Bước 3: Lưu file và Test Ngay!
1. **Lưu file lại:** Gateway hỗ trợ **Hot-Reload tức thì** (tự động phát hiện file mới thêm hoặc sửa mà không cần tắt/bật lại server).
2. **Mở trình duyệt:** Truy cập `http://localhost:8000/swagger`. Bạn sẽ thấy endpoint mock tự động xuất hiện trên Swagger UI!
3. Bấm **Try it out** $\rightarrow$ **Execute** $\rightarrow$ Nhận ngay kết quả JSON chuẩn!

---

# CÁCH 2: MOCK DỮ LIỆU CHO CONTROLLER C# CHÍNH THỨC (SEED FIXTURES)

Dành cho Backend Developer: Khi bạn đã viết xong Action trong Controller (ví dụ `ProductsController.cs`, `CartController.cs`), nhưng Backend microservice gRPC thật chưa code xong hoặc chưa bật database.

### Bước 1: Xem tên Operation trong Controller
Trong Action Controller, bạn gọi dòng lệnh Dispatcher:
```csharp
[HttpGet("catalog/brands")]
public Task<IResult> ListBrands([FromQuery] PaginationRequest pagination)
{
    return _dispatcher.DispatchPagedAsync(
        HttpContext, "catalog", "brands", ...); // <-- Tên service: "catalog", tên operation: "brands"
}
```

### Bước 2: Tạo file JSON tương ứng tại thư mục `Seeds/`
Quy tắc đường dẫn file:  
`Nexus.Gateway/Seeds/{tên_service}/{tên_operation}.json`

*Ví dụ:* `Nexus.Gateway/Seeds/catalog/brands.json`

Điền dữ liệu mẫu JSON:
```json
{
  "items": [
    { "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "brand-02", "name": "Logitech", "logoUrl": "https://example.com/logitech.png" }
  ],
  "total": 2
}
```

### Bước 3: Đảm bảo Service đang ở chế độ Mock
Mở file `Nexus.Gateway/gateway.runtime.json`:
```json
"catalog": {
  "Mode": "Mock"
}
```
*(Nếu đang là `"Real"`, đổi thành `"Mock"` và lưu lại. Gateway sẽ tự động lấy dữ liệu từ file JSON bạn vừa tạo).*

---

# 🔑 BẢNG TRA CỨU CÁC TRƯỜNG CẤU HÌNH TRONG FILE MOCK

| Trường (Field) | Bắt buộc? | Giá trị hợp lệ | Ý nghĩa |
| :--- | :---: | :--- | :--- |
| **`id`** | Có | Chuỗi bất kỳ, viết thường nối gạch ngang | Định danh duy nhất của mock endpoint (ví dụ: `mock-user-profile`). |
| **`service`** | Có | `users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet` | Service phụ trách endpoint này. |
| **`method`** | Có | `GET`, `POST`, `PUT`, `PATCH`, `DELETE` | Phương thức HTTP của API. |
| **`path`** | Có | `/api/v1/...` (hỗ trợ biến URL `{id}`) | Đường dẫn URL của API. |
| **`description`** | Không | Chuỗi mô tả ngắn | Hiển thị làm chú thích trên Swagger UI. |
| **`policy`** | Không | `public`, `authenticated`, `seller`, `staff`, `admin` | Phân quyền bảo vệ endpoint (xem chi tiết bên dưới). Mặc định là `public`. |
| **`statusCode`** | Có | `200`, `201`, `400`, `404`, `500`... | HTTP status code trả về cho Client. |
| **`responseType`** | Không | `single` (mặc định), `paged`, `raw` | Kiểu đóng gói phản hồi (xem giải thích bên dưới). |
| **`response`** | Có | Object `{ ... }` | Dữ liệu JSON thực tế trả về. |

---

### Ý Nghĩa Của `responseType`:
- **`"single"` (Khuyên dùng)**: Gateway tự động bọc dữ liệu trong envelope chuẩn `ApiResponse<T>`:
  `{"success": true, "statusCode": 200, "message": "...", "data": { ... }, "traceId": "...", "timestamp": "..."}`.
- **`"paged"`**: Dành cho danh sách phân trang. Gateway tự động bọc trong `data.items` và `data.pagination` (`pageIndex`, `pageSize`, `totalCount`, `totalPages`).
- **`"raw"`**: Trả về dữ liệu thô nguyên bản trong trường `response`, không bọc thêm bất kỳ vỏ bọc nào (thường dùng khi giả lập mã lỗi RFC Problem Details).

---

### Ý Nghĩa Của Phân Quyền `policy`:
- **`"public"`**: API công khai, bất kỳ ai cũng gọi được mà không cần Token đăng nhập.
- **`"authenticated"`**: Yêu cầu phải gửi JWT Bearer Token (bất kỳ ai đã đăng nhập).
- **`"seller"`**: Chỉ tài khoản có vai trò Người bán (Seller) mới gọi được.
- **`"staff"`**: Chỉ nhân viên hỗ trợ (Support Staff) mới gọi được.
- **`"admin"`**: Chỉ Quản trị viên (Admin) mới gọi được.

---

# ⚡ MẸO HAY DÀNH CHO DEVELOPER (TIPS & TRICKS)

### 1. Test Mock Nhanh Không Cần Đăng Nhập (Bypass Auth Mode)
Khi bạn đang làm giao diện Frontend và không muốn mất công đăng nhập lấy token:
1. Mở file `Nexus.Gateway/gateway.runtime.json`.
2. Sửa `"AuthMode": "Bypass"`.
3. Lưu file $\rightarrow$ Giờ đây bạn có thể gọi **mọi API mock (kể cả có `policy: "authenticated"` hay `"seller"`)** mà không bị chặn lỗi 401/403!

### 2. Ép Nạp Lại Mock Ngay Lập Tức
Nếu bạn vừa sửa file mock mà muốn Gateway cập nhật ngay lập tức không cần đợi debounce:
- Gửi request `POST http://localhost:8000/api/v1/mock-engine/reload` (hoặc bấm nút này trên Swagger).
- Gateway sẽ quét lại toàn bộ thư mục `Mocks/` và báo số lượng endpoint đã nạp.

### 3. Đổi Riêng 1 Request Sang Backend Thật (Header Override)
Nếu bạn đang để service ở `Mock` nhưng muốn test thử 1 request gọi sang Backend thật:
- Đính kèm Header trong Postman/cURL:
  `X-Service-Mode-catalog: Real`
- Gateway sẽ gọi thẳng gRPC sang backend cho riêng request đó!

---

*Chúc các bạn phát triển tính năng và giao diện thuận lợi!*
