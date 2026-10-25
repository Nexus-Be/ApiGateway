# 02 — BẢN ĐẶC TẢ CONTROLLERS & ĐỊNH TUYẾN (CONTROLLERS & ROUTING SPEC)
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 15/15 file controller tồn tại, 8/15 controller có action (98 action: 60 action luồng User/Auth/Admin + 27 action catalog F2 + 11 action gateway/health); 7 controller stub 10 dòng, 0 action.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.

Toàn bộ các API được quản lý bởi 15 file controller `[ApiController] ControllerBase` trong `Nexus.Gateway/Controllers/`, phân bổ phẳng theo từng nghiệp vụ/tài nguyên với tiền tố chuẩn hóa:
$$\mathbf{/api/v1/...}$$

Đếm thật: `grep -c '\[Http'` trên từng file cho ra **98 attribute action**, nằm trong 8 file: `AuthController.cs` (10), `UsersController.cs` (26), `AdminUsersController.cs` (24), `GatewayControlController.cs` (6), `HealthProberController.cs` (5), `ProductsController.cs` (20), `CategoriesController.cs` (5), `InventoryController.cs` (2). 7 file còn lại chỉ có `[ApiController]` + `[Route(...)]` + comment `// TODO (Team)` — **không có action nào**, nên request tới các route đó rơi xuống declarative mock (nếu có `Mocks/*.mock.json` khớp) hoặc trả **404**.

---

## 1. BẢNG TỔNG HỢP 15 CONTROLLER

| # | Controller | Route Prefix (thật) | Số action | Trạng thái | Bằng chứng |
|:--:|:---|:---|:--:|:---|:---|
| 1 | `AuthController` | `/api/v1/auth` | 10 | ✅ CÓ | `Controllers/AuthController.cs` |
| 2 | `UsersController` | `/api/v1` | 26 | ✅ CÓ | `Controllers/UsersController.cs` |
| 3 | `AdminUsersController` | `/api/v1/admin` | 24 | ✅ CÓ | `Controllers/AdminUsersController.cs` |
| 4 | `GatewayControlController` | `/api/v1` | 6 | ✅ CÓ | `Controllers/GatewayControlController.cs` |
| 5 | `HealthProberController` | `/api/v1` | 5 | ✅ CÓ | `Controllers/HealthProberController.cs` |
| 6 | `ProductsController` | `/api/v1` | 20 | ✅ CÓ | `Controllers/ProductsController.cs` |
| 7 | `CategoriesController` | `/api/v1` | 5 | ✅ CÓ | `Controllers/CategoriesController.cs` |
| 8 | `InventoryController` | `/api/v1` | 2 | ✅ CÓ | `Controllers/InventoryController.cs` |
| 9 | `CartController` | `/api/v1/commerce/cart` | 0 | ❌ CHƯA CÓ | `Controllers/CartController.cs` (10 dòng, TODO) |
| 10 | `OrdersController` | `/api/v1/commerce/orders` | 0 | ❌ CHƯA CÓ | `Controllers/OrdersController.cs` (10 dòng, TODO) |
| 11 | `AuctionController` | `/api/v1/auction` | 0 | ❌ CHƯA CÓ | `Controllers/AuctionController.cs` (10 dòng, TODO) |
| 12 | `FulfillmentController` | `/api/v1/fulfillment` | 0 | ❌ CHƯA CÓ | `Controllers/FulfillmentController.cs` (10 dòng, TODO) |
| 13 | `NotificationController` | `/api/v1/notification` | 0 | ❌ CHƯA CÓ | `Controllers/NotificationController.cs` (10 dòng, TODO) |
| 14 | `LogController` | `/api/v1/log` | 0 | ❌ CHƯA CÓ | `Controllers/LogController.cs` (10 dòng, TODO) |
| 15 | `WalletController` | `/api/v1/wallet` | 0 | ❌ CHƯA CÓ | `Controllers/WalletController.cs` (10 dòng, TODO) |

---

## 2. CHI TIẾT 8 CONTROLLER ĐÃ CÓ ACTION

### 1. `AuthController` (`Controllers/AuthController.cs`)
- Route Prefix: `/api/v1/auth` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (10 routes):
  - `POST /register` [AllowAnonymous] $\rightarrow$ Đăng ký tài khoản mới (`RegisterRequestDto`).
  - `POST /verify-otp` & `POST /otp/verify` [AllowAnonymous] $\rightarrow$ Xác thực tài khoản bằng OTP (`VerifyOtpRequestDto`).
  - `POST /resend-otp` & `POST /otp/resend` [AllowAnonymous] $\rightarrow$ Gửi lại mã OTP (`ResendOtpRequestDto`).
  - `POST /login` [AllowAnonymous] $\rightarrow$ Đăng nhập lấy access/refresh token (`LoginRequestDto`).
  - `POST /refresh` [AllowAnonymous] $\rightarrow$ Làm mới access token bằng refresh token (`RefreshTokenDto`).
  - `POST /password/forgot` [AllowAnonymous] $\rightarrow$ Yêu cầu đặt lại mật khẩu (`ForgotPasswordDto`).
  - `POST /password/reset` [AllowAnonymous] $\rightarrow$ Đặt lại mật khẩu mới bằng OTP (`ResetPasswordDto`).
  - `POST /password/change` & `PATCH /password` [Authorize(Authenticated)] $\rightarrow$ Đổi mật khẩu (`ChangePasswordDto`).
  - `POST /logout` [Authorize(Authenticated)] $\rightarrow$ Đăng xuất phiên hiện tại.
  - `POST /logout-all` [Authorize(Authenticated)] $\rightarrow$ Thu hồi toàn bộ phiên đăng nhập.

### 2. `UsersController` (`Controllers/UsersController.cs`)
- Route Prefix: `/api/v1` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (26 routes):
  - `GET /me/sessions` [Authenticated] $\rightarrow$ Danh sách các phiên đăng nhập.
  - `DELETE /me/sessions/{id}` [Authenticated] $\rightarrow$ Thu hồi 1 phiên đăng nhập (204 NoContent).
  - `GET /me` & `GET /users/me` [Authenticated] $\rightarrow$ Lấy thông tin cá nhân hiện tại.
  - `PATCH /me` [Authenticated] $\rightarrow$ Cập nhật thông tin cá nhân (`UpdateMyProfileDto`).
  - `GET /me/avatar` [Authenticated] $\rightarrow$ Lấy avatar người dùng hiện tại.
  - `POST /me/avatar` [Authenticated] $\rightarrow$ Tải lên avatar mới (Multipart Form).
  - `GET /me/reputation` [Authenticated] $\rightarrow$ Điểm uy tín cá nhân.
  - `GET /me/penalties` [Authenticated] $\rightarrow$ Lịch sử vi phạm/phạt (paged).
  - `GET /user/{userId}` [AllowAnonymous] $\rightarrow$ Xem hồ sơ công khai của người dùng.
  - `GET /user/{userId}/avatar` [AllowAnonymous] $\rightarrow$ Lấy avatar công khai.
  - `GET /seller/{sellerId}` [AllowAnonymous] $\rightarrow$ Xem thông tin cửa hàng người bán.
  - `GET /user/{userId}/reputation` [AllowAnonymous] $\rightarrow$ Xem điểm uy tín công khai.
  - `GET /user/{userId}/reputation/history` [Authenticated] $\rightarrow$ Lịch sử thay đổi điểm uy tín (paged).
  - `GET /user/{userId}/reviews` [AllowAnonymous] $\rightarrow$ Đánh giá của người dùng (paged).
  - `POST /review/files/upload` [Authenticated] $\rightarrow$ Tải lên ảnh/video bằng chứng đánh giá.
  - `POST /reviews` [Authenticated] $\rightarrow$ Gửi đánh giá mới (201 Created).
  - `POST /report/files/upload` [Authenticated] $\rightarrow$ Tải lên tài liệu khiếu nại.
  - `POST /users/reports` [Authenticated] $\rightarrow$ Tạo báo cáo/khiếu nại người dùng (201 Created).
  - `GET /me/reports` [Authenticated] $\rightarrow$ Danh sách khiếu nại đã gửi (paged).
  - `POST /seller/registration` [Authenticated] $\rightarrow$ Đăng ký mở cửa hàng người bán (201 Created).
  - `POST /seller/registration/otp/resend` [Authenticated] $\rightarrow$ Gửi lại mã OTP xác minh đăng ký bán hàng.
  - `POST /seller/registration/documents` [Authenticated] $\rightarrow$ Tải lên tài liệu CCCD/GPKD (Multipart).
  - `POST /seller/otp/verify` [Authenticated] $\rightarrow$ Xác minh OTP đăng ký bán hàng.
  - `GET /seller/registration` [Authenticated] $\rightarrow$ Tra cứu trạng thái duyệt hồ sơ bán hàng.
  - `GET /seller/profile` [Seller] $\rightarrow$ Lấy thông tin hồ sơ cửa hàng của chính mình.
  - `PATCH /seller/profile` [Seller] $\rightarrow$ Cập nhật thông tin cửa hàng.

### 3. `AdminUsersController` (`Controllers/AdminUsersController.cs`)
- Route Prefix: `/api/v1/admin` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (24 routes):
  - `GET /seller-registrations` [Staff] $\rightarrow$ Danh sách đơn đăng ký bán hàng chờ duyệt (paged).
  - `GET /seller-registrations/{registrationId}` [Staff] $\rightarrow$ Chi tiết hồ sơ đăng ký bán hàng.
  - `PUT /seller-registrations/{registrationId}/approve` [Staff] $\rightarrow$ Phê duyệt đơn đăng ký bán hàng.
  - `PUT /seller-registrations/{registrationId}/reject` [Staff] $\rightarrow$ Từ chối đơn đăng ký bán hàng.
  - `GET /reports` [Staff] $\rightarrow$ Danh sách báo cáo vi phạm (paged).
  - `PUT /reports/{reportId}/resolve` [Staff] $\rightarrow$ Xử lý báo cáo vi phạm.
  - `PUT /reports/{reportId}/dismiss` [Staff] $\rightarrow$ Bác bỏ báo cáo vi phạm.
  - `PUT /reviews/{reviewId}/hide` [Staff] $\rightarrow$ Ẩn đánh giá vi phạm.
  - `GET /user` & `GET /users` [Staff] $\rightarrow$ Danh sách người dùng quản trị (paged).
  - `POST /user` & `POST /users` [Admin] $\rightarrow$ Tạo tài khoản người dùng/nhân viên mới (201 Created).
  - `GET /user/{userId}` & `GET /users/{userId}` [Staff] $\rightarrow$ Chi tiết người dùng.
  - `PUT /user/{userId}` & `PUT /users/{userId}` [Admin] $\rightarrow$ Cập nhật thông tin người dùng.
  - `DELETE /user/{userId}` & `DELETE /users/{userId}` [Admin] $\rightarrow$ Xóa người dùng (204 NoContent).
  - `PATCH /user/{userId}/ban` & `PATCH /users/{userId}/ban` [Admin] $\rightarrow$ Khóa tài khoản người dùng.
  - `PATCH /user/{userId}/unban` & `PATCH /users/{userId}/unban` [Admin] $\rightarrow$ Mở khóa tài khoản.
  - `GET /sellers/{sellerId}` [Staff] $\rightarrow$ Chi tiết thông tin người bán phía quản trị.
  - `PUT /user/{userId}/roles` & `PUT /users/{userId}/roles` [Admin] $\rightarrow$ Gán vai trò cho người dùng.
  - `PATCH /user/{userId}/reputation` & `PATCH /users/{userId}/reputation` [Admin] $\rightarrow$ Điều chỉnh điểm uy tín thủ công.
  - `GET /roles` [Admin] $\rightarrow$ Danh sách vai trò trong hệ thống.
  - `GET /permissions` [Admin] $\rightarrow$ Danh sách quyền hạn.
  - `PUT /roles/{roleId}/permissions` [Admin] $\rightarrow$ Cập nhật phân quyền cho vai trò.
  - `GET /audit-logs` [Admin] $\rightarrow$ Danh sách nhật ký kiểm toán hệ thống (paged).
  - `GET /configs` [Admin] $\rightarrow$ Danh sách cấu hình hệ thống.
  - `PUT /configs/{key}` [Admin] $\rightarrow$ Cập nhật cấu hình hệ thống.

### 4. `GatewayControlController` (`Controllers/GatewayControlController.cs`)
- Route Prefix: `/api/v1` | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/modes` $\rightarrow$ Lấy `AuthMode` và trạng thái Mock/Real của 8 services.
  - `PUT /gateway/mode` $\rightarrow$ Chuyển `AuthMode` (`Enforce`/`Standard` vs `Bypass`; `Bypass` chỉ hợp lệ ở Development — body `AuthModeUpdateRequestDto`).
  - `PUT /gateway/modes/{service}` $\rightarrow$ Chuyển chế độ Mock/Real cho 1 service (`ServiceModeUpdateRequestDto`).
  - `PUT /gateway/modes` $\rightarrow$ Cập nhật hàng loạt chế độ các service (`BulkServiceModeUpdateRequestDto`).
  - `GET /mock-engine/definitions` $\rightarrow$ Xem danh sách declarative mock endpoints.
  - `POST /mock-engine/reload` $\rightarrow$ Quét và nạp lại mock definitions nóng.

### 5. `HealthProberController` (`Controllers/HealthProberController.cs`)
- Route Prefix: `/api/v1` (route con `gateway/prober/...`) | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/prober/status` $\rightarrow$ Trạng thái prober, failure count, latency, recent samples của 8 services.
  - `GET /gateway/prober/status/{service}` $\rightarrow$ Chi tiết trạng thái prober của 1 service.
  - `PUT /gateway/prober/{service}` $\rightarrow$ Bật/tắt prober daemon cho 1 service (`ServiceProberToggleDto`).
  - `PUT /gateway/prober/bulk` $\rightarrow$ Bật/tắt hàng loạt prober daemons (`BulkProberToggleDto`).
  - `POST /gateway/prober/{service}/check` $\rightarrow$ **Kiểm tra sức khỏe thủ công tức thì** — probe ngay 1 lần và trả về latency/status code.

### 6. `ProductsController` (`Controllers/ProductsController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (20 routes khớp hợp đồng F2 quản lý sản phẩm):
  - `GET /products` | `products` | `AllowAnonymous`
  - `GET /product/search` | `product-search` | `Seller`
  - `GET /product/{productId}` | `product` | `AllowAnonymous`
  - `POST /products` | `product-create` (201) | `Seller`
  - `PATCH /product/{productId}` | `product-update` | `Seller`
  - `DELETE /product/{productId}` | `product-delete` (204) | `Seller`
  - `POST /product/{productId}pics` | `product-images-add` (201) | `Seller`
  - `GET /product/{productId}pics` | `product-images` | `AllowAnonymous`
  - `DELETE /product/{productId}pics?ids=` | `product-images-delete` (204) | `Seller`
  - `GET /products/{productId}/totalRating` | `product-rating-total` | `AllowAnonymous`
  - `GET /products/{productId}/rating` | `product-rating` | `AllowAnonymous`
  - `PATCH /product/{productId}/status` | `product-status` | `Seller`
  - `POST /product/{productId}/variants` | `variant-create` (201) | `Seller`
  - `DELETE /variant/{variantId}` | `variant-delete` (204) | `Seller`
  - `PATCH /variant/{variantId}` | `variant-update` | `Seller`
  - `GET /variant/{variantId}` | `variant` | `Seller`
  - `POST /product/auction` | `auction-product-create` (201) | `Seller`
  - `GET /product/auction/search` | `auction-product-search` | `Seller`
  - `GET /product/auctions` | `auction-products` | `Seller`
  - `GET /product/auction/{auctionId}` | `auction-product` | `Seller`

### 7. `CategoriesController` (`Controllers/CategoriesController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (5 routes danh mục F2):
  - `GET /categories` | `categories` | `AllowAnonymous`
  - `GET /categories/{categoryId}` | `category` | `AllowAnonymous`
  - `POST /categories` | `category-create` (201) | `Admin`
  - `PUT /categories/{categoryId}` | `category-update` | `Admin`
  - `DELETE /categories/{categoryId}` | `category-delete` (204) | `Admin`

### 8. `InventoryController` (`Controllers/InventoryController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (2 routes kho F2):
  - `POST /inventory/stock-in` | `inventory-stock-in` | `Seller`
  - `POST /variant/{variantId}/stock-in` | `variant-stock-in` | `Seller`

- Validate: `Common/CatalogValidation.cs`, `Common/UserValidation.cs`
- Test: `Nexus.Gateway.Tests/CatalogMockTests.cs` (42 cases), `CatalogSeedConformanceTests.cs` (27 cases), `UserMockTests.cs` (34 cases), `UserSeedConformanceTests.cs` (55 cases).

---

## 3. ĐẶC TẢ MỤC TIÊU CHO 7 CONTROLLER STUB (CHƯA TRIỂN KHAI)
Các route dưới đây là **đặc tả mục tiêu, chưa triển khai** — không controller nào trong danh sách này có action, và hầu hết cũng chưa có RPC tương ứng trong `Nexus.Gateway/Contracts/shop.proto`.

### 9. `CartController` (`Controllers/CartController.cs`)
- Route Prefix: `/api/v1/commerce/cart` | Service: `commerce`
- Đặc tả mục tiêu: `GET /commerce/cart`, `POST /commerce/cart/items`, `PATCH /commerce/cart/items/{productId}`, `DELETE /commerce/cart/items/{productId}`, `DELETE /commerce/cart`.

### 10. `OrdersController` (`Controllers/OrdersController.cs`)
- Route Prefix: `/api/v1/commerce/orders` | Service: `commerce`
- Đặc tả mục tiêu: `GET /commerce/orders`, `GET /commerce/orders/{id}`, `POST /commerce/orders` (bắt buộc `Idempotency-Key`), `GET /seller/orders`, `GET /admin/orders`.

### 11. `AuctionController` (`Controllers/AuctionController.cs`)
- Route Prefix: `/api/v1/auction` | Service: `auction`
- Đặc tả mục tiêu: Triển khai theo luồng F3 Đấu giá (`feat/auction-mock-api`).

### 12. `FulfillmentController` (`Controllers/FulfillmentController.cs`)
- Route Prefix: `/api/v1/fulfillment` | Service: `fulfillment`
- Đặc tả mục tiêu: `GET /inventory/{skuId}`, `POST /shipping/quote`, `GET /shipments/{trackingCode}`.

### 13. `NotificationController` (`Controllers/NotificationController.cs`)
- Route Prefix: `/api/v1/notification` | Service: `notification`
- Đặc tả mục tiêu: `GET /notification`, `PUT /notification/{id}/read`, `GET /notification/preferences`, `PUT /notification/preferences`.

### 14. `LogController` (`Controllers/LogController.cs`)
- Route Prefix: `/api/v1/log` | Service: `log`
- Đặc tả mục tiêu: `GET /log/logs`, `GET /log/logs/{id}` [staff], `GET /log/event-types` [staff], `PUT /log/event-types/{code}` [admin].
- Ghi chú: endpoint `POST /api/v1/log/batch` (đích của `Logging/GatewayBatchLogSink.cs`) **không tồn tại** ở cả Gateway và Log service (Log.Api chỉ map `/health` và `/`) $\rightarrow$ batch log hiện POST vào route 404 và bỏ qua kết quả.

### 15. `WalletController` (`Controllers/WalletController.cs`)
- Route Prefix: `/api/v1/wallet` | Service: `wallet`
- Đặc tả mục tiêu: `GET /wallet/balance`, `POST /wallet/deposit`, `POST /wallet/withdraw`, `POST /wallet/transfer`, `GET /wallet/transactions`.
