# Nexus API Gateway (.NET 8)

API Gateway hoàn chỉnh cho 7 Microservices theo các quyết định trong [ADR.md](ADR.md) và chuẩn định dạng dữ liệu trong [input.md](input.md), tích hợp **Gateway State Switching** (Standard vs Mock Bypass) và **Declarative Mock Engine**.

---

## 1. Cơ chế 2 Trạng thái Gateway (Gateway State: Standard vs Bypass)

Gateway hỗ trợ 2 trạng thái hoạt động chính giúp team vừa có thể kiểm tra quy trình xác thực chuẩn xác, vừa có thể phát triển giao diện nhanh với Mock độc lập:

### a. Trạng thái Standard (`"Standard"` - Mặc định)
- **Vẫn cần đăng nhập và kiểm tra quyền mọi thứ như thường**:
  - Khi người dùng/FE gọi các API yêu cầu đăng nhập mà **chưa gửi token**: Gateway chặn lại với mã `401 Unauthorized`.
  - Người dùng đăng nhập qua `POST /api/v1/auth/login` để nhận access token:
    ```json
    { "username": "buyer01", "password": "Passw0rd!" }
    ```
  - Gửi header `Authorization: Bearer <token>` ➔ Được xác thực thành công `200 OK`.
  - Nếu token không đủ quyền (ví dụ token người mua gọi endpoint quản trị `/api/v1/admin/roles`) ➔ Bị chặn với mã `403 Forbidden`.

### b. Trạng thái Mock Bypass (`"Bypass"`)
- Khi chuyển Gateway sang trạng thái này:
  - **Không kiểm tra auth**: Không bắt buộc phải đăng nhập, không yêu cầu token.
  - **Các API mock chạy hoàn toàn độc lập**: Bạn có thể gọi bất kỳ API nào (kể cả `/users/me`, `/seller/inventory`, `/admin/roles`) mà không bị ràng buộc vào service user hay login.
  - Phù hợp khi Frontend dev muốn code màn hình nhanh mà không muốn phải login đi login lại.

---

## 2. Cách Chuyển đổi Trạng thái Gateway

### Cách 1: Qua API Runtime (Frontend có thể gạt công tắc toggle trên web)
- **Xem trạng thái hiện tại**:
  ```http
  GET /api/v1/gateway/modes
  ```
  Trả về:
  ```json
  {
    "success": true,
    "statusCode": 200,
    "data": {
      "gatewayMode": "Standard",
      "services": {
        "catalog": { "service": "catalog", "mode": "Mock" },
        "commerce": { "service": "commerce", "mode": "Mock" },
        "users": { "service": "users", "mode": "Mock" },
        "auction": { "service": "auction", "mode": "Mock" },
        "fulfillment": { "service": "fulfillment", "mode": "Mock" },
        "notification": { "service": "notification", "mode": "Mock" },
        "audit": { "service": "audit", "mode": "Mock" }
      }
    }
  }
  ```

- **Chuyển sang trạng thái Mock Bypass (Không check auth, API độc lập)**:
  ```http
  PUT /api/v1/gateway/mode
  Content-Type: application/json

  { "mode": "Bypass" }
  ```

- **Chuyển về trạng thái Standard (Yêu cầu đăng nhập như thường)**:
  ```http
  PUT /api/v1/gateway/mode
  Content-Type: application/json

  { "mode": "Standard" }
  ```

### Cách 2: Qua Request Header (Test tạm thời trên 1 request)
- Gửi header `X-Gateway-Auth: Bypass` trên request để tạm thời bỏ qua kiểm tra auth cho riêng request đó.

### Cách 3: Qua File cấu hình `gateway.runtime.json`
Chỉ cần sửa giá trị `"GatewayMode": "Standard"` thành `"GatewayMode": "Bypass"` (áp dụng ngay mà không cần restart server).

---

## 3. Declarative Mock Engine (Tạo Mock API Không Cần Code C#)

Thành viên team chỉ cần tạo file `*.mock.json` trong thư mục `Nexus.Gateway/Mocks/`:
```json
{
  "method": "POST",
  "path": "/api/v1/catalog/search",
  "description": "Tìm kiếm sản phẩm",
  "policy": "public",
  "statusCode": 200,
  "responseType": "paged",
  "request": {
    "keyword": "bàn phím",
    "minPrice": 500000
  },
  "response": {
    "items": [
      { "id": "22222222-2222-2222-2222-000000000001", "name": "Bàn phím cơ", "price": "1250000.00" }
    ],
    "total": 1
  }
}
```

- **Tự động nhận diện Service**: Đường dẫn `/api/v1/catalog/...` tự map vào service `catalog`.
- **Tự động hiện trên Swagger UI**: Không cần sửa code C#, Swagger tự sinh tài liệu API đầy đủ.
- **Hot-Reload tức thì**: Sửa file JSON là có hiệu lực ngay mà không cần restart server.

---

## 4. Hướng dẫn Chạy & Kiểm thử

```powershell
# Chạy bộ test suite toàn diện (46/46 tests pass 100%)
dotnet test ApiGateway.sln -c Release

# Khởi chạy Gateway
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet run --project Nexus.Gateway --no-launch-profile --urls http://127.0.0.1:5200
```
Mở trình duyệt truy cập: `http://localhost:5200/` (tự động chuyển hướng sang Swagger UI).
