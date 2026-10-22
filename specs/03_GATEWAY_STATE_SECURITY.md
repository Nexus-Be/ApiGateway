# 03 — BẢO MẬT & TRẠNG THÁI RUNTIME CỦA GATEWAY (SECURITY & RUNTIME MODES)

---

## 1. HAI TRẠNG THÁI HOẠT ĐỘNG (GATEWAY MODES)

```
                       ┌──────────────────────────────┐
                       │  Client Request Đến Gateway  │
                       └──────────────┬───────────────┘
                                      │
                         [Kiểm tra GatewayMode]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
      [ Mode: "Standard" (Mặc định) ]         [ Mode: "Bypass" (Development) ]
                   │                                     │
       Bắt buộc phải có JWT Token hợp lệ      Nếu gọi API Mock: Bỏ qua kiểm tra auth,
       trên header Authorization: Bearer.     tự động gán danh tính giả lập tương ứng.
       Không có token -> 401 Unauthorized.    ────────────────────────────────────────
       Sai quyền -> 403 Forbidden.            Ngoại lệ: /gateway/... và /mock-engine/
                                              vẫn bắt buộc Token Admin thật.
```

1. **Chế Độ Standard (`"Standard"`)**:
   - Mọi request yêu cầu đăng nhập bắt buộc phải mang JWT Token hợp lệ do `UserService` cấp.
   - Nếu token không đủ quyền (ví dụ role `buyer` gọi API quản trị `/admin/users`) $\rightarrow$ Bị chặn ngay với mã `403 Forbidden`.

2. **Chế Độ Mock Bypass (`"Bypass"`)**:
   - Phục vụ Frontend team phát triển giao diện nhanh chóng, độc lập.
   - Không cần đăng nhập, không cần gửi token; Gateway tự động gán danh tính `ClaimsPrincipal` thích ứng để vượt qua bộ lọc RBAC một cách mượt mà.
   - **Chặn Bypass Tuyệt Đối**: Các API điều khiển hệ thống (`/api/v1/gateway/...`, `/api/v1/mock-engine/...` và `/api/v1/gateway/prober/...`) được đánh dấu thuộc tính `[DisableMockAuthorizationBypass]`, tuyệt đối không thể bypass và luôn yêu cầu JWT Admin thật.

---

## 2. CHUYỂN ĐỔI CHẾ ĐỘ MOCK VÀ REAL (SERVICE-LEVEL SWITCHING)

Gateway hỗ trợ chuyển đổi từng service độc lập mà không cần restart server:

1. **Hot-Reload Qua File `gateway.runtime.json`**:
   ```json
   {
     "AuthMode": "Standard",
     "Services": {
       "catalog": { "Mode": "Real", "Address": "http://localhost:8002" },
       "commerce": { "Mode": "Mock" }
     }
   }
   ```
2. **Chuyển Đổi Qua API Runtime**:
   ```http
   PUT /api/v1/gateway/modes/catalog
   Content-Type: application/json

   { "mode": "Real" }
   ```
3. **Override Cho Từng Request Qua Header (Development)**:
   - Gửi header `X-Service-Mode-catalog: Real` để ép request đó gọi backend thật.
   - Gửi header `X-Mock-Enforce-Auth: true` để buộc kiểm tra auth nghiêm ngặt trên endpoint mock.

---

## 3. GIỚI HẠN TẦN SUẤT GỌI PHÂN TÁN (DISTRIBUTED RATE LIMITING)

- Triển khai trong [RedisRateLimitMiddleware.cs](../Nexus.Gateway/Middleware/RedisRateLimitMiddleware.cs).
- Phân vùng theo IP Client và UserId (nếu đã đăng nhập).
- Thuật toán: Fixed/Sliding Window dựa trên Redis atomic commands.
- Mã lỗi trả về khi vượt ngưỡng: `429 Too Many Requests` đi kèm header `Retry-After: <seconds>`.
