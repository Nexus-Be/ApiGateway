# PLAN — Kế hoạch & Tiến trình Mở rộng Nexus API Gateway (Gateway Mode State)

Tài liệu này ghi nhận kế hoạch tổng thể, các kế hoạch con (sub-plans), tiêu chí nghiệm thu (checkpoints), bằng chứng kiểm thử từng phần và kết quả chạy thử nghiệm thực tế (live run E2E) bổ sung trạng thái Gateway Mode linh hoạt giữa kiểm tra đăng nhập bình thường và chế độ Mock độc lập.

---

## 0. Đối chiếu Yêu cầu Nghiệp vụ

1. **Trạng thái Mặc định (`GatewayMode: "Standard"`)**:
   - Vẫn cần đăng nhập và kiểm tra quyền mọi thứ như thường.
   - Chưa đăng nhập (không gửi token) ➔ Bị chặn với mã `401 Unauthorized`.
   - Đăng nhập (`POST /api/v1/auth/login`) lấy token ➔ Gửi token vào request để truy cập `200 OK`.
   - Sai vai trò/quyền (ví dụ buyer gọi endpoint admin) ➔ Bị chặn với mã `403 Forbidden`.
2. **Trạng thái Mock (`GatewayMode: "Bypass"`)**:
   - Bật khi team muốn phát triển giao diện nhanh hoặc test độc lập.
   - **KHÔNG CHECK AUTH**: Không bắt buộc đăng nhập, tự động cấp quyền tương ứng cho các API.
   - **CÁC API ĐỘC LẬP**: Các API mock chạy hoàn toàn độc lập mà không bị ràng buộc vào login hay service user.
   - Dễ dàng bật/tắt qua file cấu hình `gateway.runtime.json` hoặc gọi API `PUT /api/v1/gateway/mode`.

---

## 1. Kế hoạch Tổng thể & Các Kế hoạch Con (Phases)

- **Plan 1: Bổ sung trạng thái GatewayMode trong GatewayOptions, config và IGatewayRuntimeManager**
  - [x] Thêm thuộc tính `GatewayMode` (`"Standard"` | `"Bypass"`) vào `GatewayOptions` và `gateway.runtime.json`.
  - [x] Cập nhật `IGatewayRuntimeManager` và `GatewayRuntimeManager` quản lý `GetGatewayMode`, `SetGatewayModeAsync`, `IsAuthBypassed`.
  - *Checkpoint*: Build thành công.

- **Plan 2: Cập nhật Security theo 2 trạng thái**
  - [x] Khi `Standard`: Giữ nguyên kiểm tra đăng nhập và phân quyền JWT/RBAC bình thường.
  - [x] Khi `Bypass`: Tự động nới lỏng auth và cấp quyền thích ứng để các API mock chạy độc lập.
  - *Checkpoint*: Build thành công.

- **Plan 3: Nâng cấp API Gateway Modes**
  - [x] `GET /api/v1/gateway/modes`: Trả về cả `gatewayMode` và danh sách `services`.
  - [x] `PUT /api/v1/gateway/mode`: Cho phép bật/tắt runtime giữa `Standard` và `Bypass` (`{"mode": "Bypass"}`).
  - [x] `PUT /api/v1/gateway/modes`: Hỗ trợ cập nhật đồng thời nhiều service và gateway mode.
  - *Checkpoint*: Build thành công.

- **Plan 4: Nâng cấp Mock Login sinh token theo tài khoản**
  - [x] `POST /api/v1/auth/login` trả về token hợp lệ cho mock flow.
  - *Checkpoint*: Build thành công.

- **Plan 5: Viết Test Suite tự động kiểm chứng cả 2 trạng thái**
  - [x] Bổ sung `Gateway_Standard_mode_strictly_requires_login_as_normal` và `Gateway_Bypass_mode_skips_auth_check_and_runs_independently`.
  - [x] Chạy `dotnet test ApiGateway.sln -c Release`: 46/46 tests pass 100%.
  - *Checkpoint*: Toàn bộ 40 tests vượt qua.

- **Plan 6: Chạy live server Gateway, test trực tiếp cả 2 luồng và nghiệm thu**
  - [x] Chạy live server Gateway thực tế cổng 5200:
    - Luồng Standard: Gọi không token ➔ 401; Đăng nhập lấy token ➔ 200 OK.
    - Luồng Bypass: Đổi mode sang `Bypass` ➔ Gọi `/users/me`, `/seller/inventory`, `/admin/roles` không cần token đều đạt 200 OK độc lập.
    - Reset về Standard: Gọi lại không token ➔ Lập tức bị chặn 401 như thường.
