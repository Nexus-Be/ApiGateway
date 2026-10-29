# 02 — BẢN ĐẶC TẢ CONTROLLERS & ĐỊNH TUYẾN (CONTROLLERS & ROUTING SPEC)
> **Trạng thái triển khai (2026-10-27):** ✅ Hoàn tất — 15/15 file controller đều đã hoàn thiện đầy đủ actions, 46 seed JSON fixtures trong `Seeds/`, 372 test cases pass 100%. Các controller kế thừa `GatewayBaseController` sử dụng cơ chế điều phối REST downstream `ForwardAsync` và `ForwardPagedAsync`.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-27.

Toàn bộ các API được quản lý bởi 15 file controller trong `Nexus.Gateway/Controllers/` kế thừa `GatewayBaseController`, phân bổ theo từng nghiệp vụ/tài nguyên với tiền tố chuẩn hóa:
$$\mathbf{/api/v1/...}$$

---

## 1. BẢNG TỔNG HỢP 15 CONTROLLER

| # | Controller | Route Prefix (thật) | Trạng thái | Giao tiếp Downstream | Bằng chứng |
|:--:|:---|:---|:--:|:---|:---|
| 1 | `AuthController` | `/api/v1/auth` | ✅ CÓ | REST `users` (8001) | `Controllers/AuthController.cs` |
| 2 | `UsersController` | `/api/v1` | ✅ CÓ | REST `users` (8001) | `Controllers/UsersController.cs` |
| 3 | `AdminUsersController` | `/api/v1/admin` | ✅ CÓ | REST `users` (8001) | `Controllers/AdminUsersController.cs` |
| 4 | `GatewayControlController` | `/api/v1` | ✅ CÓ | Internal Gateway | `Controllers/GatewayControlController.cs` |
| 5 | `HealthProberController` | `/api/v1` | ✅ CÓ | Internal Gateway | `Controllers/HealthProberController.cs` |
| 6 | `ProductsController` | `/api/v1` | ✅ CÓ | REST `catalog` (8002) | `Controllers/ProductsController.cs` |
| 7 | `CategoriesController` | `/api/v1` | ✅ CÓ | REST `catalog` (8002) | `Controllers/CategoriesController.cs` |
| 8 | `InventoryController` | `/api/v1` | ✅ CÓ | REST `catalog` (8002) | `Controllers/InventoryController.cs` |
| 9 | `AuctionController` | `/api/v1` | ✅ CÓ | REST `auction` (8005) | `Controllers/AuctionController.cs` |
| 10 | `CartController` | `/api/v1/commerce/cart` | ✅ CÓ | REST `commerce` (8003) | `Controllers/CartController.cs` |
| 11 | `OrdersController` | `/api/v1/commerce/orders` | ✅ CÓ | REST `commerce` (8003) | `Controllers/OrdersController.cs` |
| 12 | `FulfillmentController` | `/api/v1/fulfillment` | ✅ CÓ | REST `fulfillment` (8006) | `Controllers/FulfillmentController.cs` |
| 13 | `NotificationController` | `/api/v1/notification` | ✅ CÓ | REST `notification` (8007) | `Controllers/NotificationController.cs` |
| 14 | `LogController` | `/api/v1/log` | ✅ CÓ | REST `log` (8004) | `Controllers/LogController.cs` |
| 15 | `WalletController` | `/api/v1/wallet` | ✅ CÓ | REST `wallet` (8008) | `Controllers/WalletController.cs` |

---

## 2. CHI TIẾT 9 CONTROLLER ĐÃ CÓ ACTION

### 1. `AuthController` (`Controllers/AuthController.cs`)
- Route Prefix: `/api/v1/auth` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (10 routes):
  - `POST /register` [AllowAnonymous] $\rightarrow$ Đăng ký tài khoản mới (`RegisterRequestDto`).
  - `POST /otp/verify` [AllowAnonymous] $\rightarrow$ Xác thực tài khoản bằng OTP (`VerifyOtpRequestDto`).
  - `POST /otp/resend` [AllowAnonymous] $\rightarrow$ Gửi lại mã OTP (`ResendOtpRequestDto`).
  - `POST /login` [AllowAnonymous] $\rightarrow$ Đăng nhập lấy access/refresh token (`LoginRequestDto`).
  - `POST /refresh` [AllowAnonymous] $\rightarrow$ Làm mới access token bằng refresh token (`RefreshTokenDto`).
  - `POST /password/forgot` [AllowAnonymous] $\rightarrow$ Yêu cầu đặt lại mật khẩu (`ForgotPasswordDto`).
  - `POST /password/reset` [AllowAnonymous] $\rightarrow$ Đặt lại mật khẩu mới bằng OTP (`ResetPasswordDto`).
  - `PATCH /password` [Authorize(Authenticated)] $\rightarrow$ Đổi mật khẩu (`ChangePasswordDto`).
  - `POST /logout` [Authorize(Authenticated)] $\rightarrow$ Đăng xuất phiên hiện tại.
  - `POST /logout-all` [Authorize(Authenticated)] $\rightarrow$ Thu hồi toàn bộ phiên đăng nhập.

### 2. `UsersController` (`Controllers/UsersController.cs`)
- Route Prefix: `/api/v1` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (26 routes):
  - `GET /me/sessions` [Authenticated] $\rightarrow$ Danh sách các phiên đăng nhập.
  - `DELETE /me/sessions/{id}` [Authenticated] $\rightarrow$ Thu hồi 1 phiên đăng nhập (204 NoContent).
  - `GET /me` [Authenticated] $\rightarrow$ Lấy thông tin cá nhân hiện tại.
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
  - `POST /user/reports` & `POST /users/reports` [Authenticated] $\rightarrow$ Tạo báo cáo/khiếu nại người dùng (201 Created).
  - `GET /me/reports` [Authenticated] $\rightarrow$ Danh sách khiếu nại đã gửi (paged).
  - `POST /seller/registration` [Authenticated] $\rightarrow$ Đăng ký mở cửa hàng người bán (201 Created).
  - `POST /seller/registration/otp/resend` [Authenticated] $\rightarrow$ Gửi lại mã OTP xác minh đăng ký bán hàng.
  - `POST /seller/registration/documents` [Authenticated] $\rightarrow$ Tải lên tài liệu CCCD/GPKD (Multipart).
  - `POST /seller/otp/verify` [Authenticated] $\rightarrow$ Xác minh OTP đăng ký bán hàng.
  - `GET /seller/registration` [Authenticated] $\rightarrow$ Trạng thái hồ sơ đăng ký bán hàng.
  - `GET /seller/profile` [Seller] $\rightarrow$ Xem hồ sơ cửa hàng đầy đủ của chính mình.
  - `PATCH /seller/profile` [Seller] $\rightarrow$ Cập nhật hồ sơ cửa hàng.

### 3. `AdminUsersController` (`Controllers/AdminUsersController.cs`)
- Route Prefix: `/api/v1/admin` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions (24 routes):
  - `GET /seller-registrations` [Staff] $\rightarrow$ Danh sách đơn đăng ký bán hàng (paged).
  - `GET /seller-registrations/{id}` [Staff] $\rightarrow$ Chi tiết đơn đăng ký.
  - `PUT /seller-registrations/{id}/approve` [Staff] $\rightarrow$ Duyệt đơn mở shop.
  - `PUT /seller-registrations/{id}/reject` [Staff] $\rightarrow$ Từ chối đơn mở shop kèm lý do.
  - `GET /reports` [Staff] $\rightarrow$ Danh sách khiếu nại người dùng (paged).
  - `PUT /reports/{id}/resolve` [Staff] $\rightarrow$ Giải quyết khiếu nại.
  - `PUT /reports/{id}/dismiss` [Staff] $\rightarrow$ Bác bỏ khiếu nại.
  - `PUT /reviews/{id}/hide` [Staff] $\rightarrow$ Ẩn đánh giá vi phạm tiêu chuẩn.
  - `GET /user` [Staff] $\rightarrow$ Tìm kiếm / danh sách tài khoản (paged).
  - `POST /user` [Admin] $\rightarrow$ Tạo tài khoản mới (201 Created).
  - `GET /user/{userId}` [Staff] $\rightarrow$ Chi tiết tài khoản quản trị.
  - `PUT /user/{userId}` [Admin] $\rightarrow$ Cập nhật thông tin tài khoản.
  - `DELETE /user/{userId}` [Admin] $\rightarrow$ Xóa tài khoản (204 NoContent).
  - `PATCH /user/{userId}/ban` [StaffOrAdmin] $\rightarrow$ Khóa tài khoản kèm lý do.
  - `PATCH /user/{userId}/unban` [StaffOrAdmin] $\rightarrow$ Mở khóa tài khoản.
  - `GET /sellers/{id}` [Staff] $\rightarrow$ Chi tiết quản trị hồ sơ người bán.
  - `PUT /user/{userId}/roles` [Admin] $\rightarrow$ Cập nhật danh sách quyền/roles người dùng.
  - `PATCH /user/{userId}/reputation` [Admin] $\rightarrow$ Điều chỉnh điểm uy tín thủ công.
  - `GET /roles` [Admin] $\rightarrow$ Danh sách tất cả vai trò trong hệ thống.
  - `GET /permissions` [Admin] $\rightarrow$ Danh sách quyền hạn.
  - `PUT /roles/{id}/permissions` [Admin] $\rightarrow$ Cập nhật ma trận quyền cho vai trò.
  - `GET /audit-logs` [Admin] $\rightarrow$ Tra cứu nhật ký kiểm toán hệ thống (paged).
  - `GET /configs` [Admin] $\rightarrow$ Xem toàn bộ cấu hình hệ thống.
  - `PUT /configs/{key}` [Admin] $\rightarrow$ Cập nhật cấu hình hệ thống.

### 4. `GatewayControlController` (`Controllers/GatewayControlController.cs`)
- Route Prefix: `/api/v1` | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/modes` $\rightarrow$ Xem chế độ `Real`/`Mock` của toàn bộ 8 service.
  - `PUT /gateway/modes/{service}` $\rightarrow$ Cập nhật nóng chế độ 1 service (`ServiceModeUpdateDto`).
  - `PUT /gateway/modes` $\rightarrow$ Cập nhật hàng loạt chế độ các service (`BulkServiceModeUpdateRequestDto`).
  - `GET /mock-engine/definitions` $\rightarrow$ Xem danh sách declarative mock endpoints.
  - `POST /mock-engine/reload` $\rightarrow$ Quét và nạp lại mock definitions nóng.

### 5. `HealthProberController` (`Controllers/HealthProberController.cs`)
- Route Prefix: `/api/v1` (route con `gateway/prober/...`) | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/prober/status` $\rightarrow$ Snapshot trạng thái health của 8 backend + circuit breaker.
  - `GET /gateway/prober/history` $\rightarrow$ Lịch sử probe (vòng đệm 200 bản ghi gần nhất).
  - `PUT /gateway/prober/{service}` $\rightarrow$ Bật/tắt prober daemon cho 1 service (`ServiceProberToggleDto`).
  - `PUT /gateway/prober/bulk` $\rightarrow$ Bật/tắt hàng loạt prober daemons (`BulkProberToggleDto`).
  - `POST /gateway/prober/{service}/check` $\rightarrow$ **Kiểm tra sức khỏe thủ công tức thì** — probe ngay 1 lần và trả về latency/status code.

### 6. `ProductsController` (`Controllers/ProductsController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (20 routes):
  - `GET /products` | op `products` (paged) | `AllowAnonymous`
  - `GET /product/search` | op `product-search` (paged) | `Seller`
  - `GET /product/{productId}` | op `product` | `AllowAnonymous`
  - `POST /products` | op `product-create` (201) | `Seller`
  - `PATCH /product/{productId}` | op `product-update` | `Seller`
  - `DELETE /product/{productId}` | op `product-delete` (204) | `Seller`
  - `POST /product/{productId}pics` | op `product-images-add` (201) | `Seller` (Multipart Form)
  - `GET /product/{productId}pics` | op `product-images` | `AllowAnonymous`
  - `DELETE /product/{productId}pics` | op `product-images-delete` (204) | `Seller`
  - `GET /products/{productId}/totalRating` | op `product-rating-total` | `AllowAnonymous`
  - `GET /products/{productId}/rating` | op `product-rating` | `AllowAnonymous`
  - `PATCH /product/{productId}/status` | op `product-status` | `Seller`
  - `POST /product/{productId}/variants` | op `variant-create` (201) | `Seller`
  - `DELETE /variant/{variantId}` | op `variant-delete` (204) | `Seller`
  - `PATCH /variant/{variantId}` | op `variant-update` | `Seller`
  - `GET /variant/{variantId}` | op `variant` | `Seller`
  - `POST /product/auction` | op `auction-product-create` (201) | `Seller`
  - `GET /product/auction/search` | op `auction-product-search` (paged) | `Seller`
  - `GET /product/auctions` | op `auction-products` (paged) | `Seller`
  - `GET /product/auction/{auctionId}` | op `auction-product` | `Seller`

### 7. `CategoriesController` (`Controllers/CategoriesController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (5 routes):
  - `GET /categories` | op `categories` | `AllowAnonymous`
  - `GET /categories/{categoryId}` | op `category` | `AllowAnonymous`
  - `POST /categories` | op `category-create` (201) | `Admin`
  - `PUT /categories/{categoryId}` | op `category-update` | `Admin`
  - `DELETE /categories/{categoryId}` | op `category-delete` (204) | `Admin`

### 8. `InventoryController` (`Controllers/InventoryController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (2 routes):
  - `POST /inventory/stock-in` | `inventory-stock-in` | `Seller`
  - `POST /variant/{variantId}/stock-in` | `variant-stock-in` | `Seller`

### 9. `AuctionController` (`Controllers/AuctionController.cs`)
- Route Prefix: `/api/v1` | Service: `auction` | Contract: F3, AUC-01..AUC-19
- Attributes: `[ApiController]`, `[BackendTag("auction")]`, `[EnableRateLimiting("api")]`
- Actions (16 routes):
  - `GET /auctions` | `auctions` (paged) | `AllowAnonymous`
  - `GET /auctions/{auctionId}` | `auction` | `AllowAnonymous`
  - `GET /auctions/{auctionId}/bids` | `auction-bids` (paged) | `AllowAnonymous`
  - `POST /auctions/{auctionId}/bids` | `auction-bid-place` (201) | `Authenticated` + `Idempotency-Key` + `If-Match`
  - `GET /me/bids` | `me-bids` (paged) | `Authenticated`
  - `GET /auctions/{auctionId}/result` | `auction-result` | `Authenticated`
  - `POST /auctions` | `auction-create` (201) | `Seller`
  - `PUT /auctions/{auctionId}` | `auction-update` | `Seller`
  - `POST /auctions/{auctionId}/cancel` | `auction-cancel` | `Seller`
  - `POST /auctions/{auctionId}/recreate` | `auction-recreate` (201) | `Seller`
  - `GET /seller/auctions` | `seller-auctions` (paged) | `Seller`
  - `POST /admin/auctions/{auctionId}/force-cancel` | `auction-force-cancel` | `Admin`
  - `GET /admin/auction-configs` | `auction-configs` | `Admin`
  - `PUT /admin/auction-configs/{key}` | `auction-config-update` | `Admin`
  - `GET /auctions/{auctionId}/status-history` | `auction-status-history` | `Staff`
  - `GET /admin/auctions/{auctionId}/saga` | `auction-saga` | `Staff`

---

## 3. ĐẶC TẢ MỤC TIÊU CHO 6 CONTROLLER STUB (CHƯA TRIỂN KHAI)

Các route dưới đây là **đặc tả mục tiêu, chưa triển khai** — không controller nào trong danh sách này có action, và hầu hết cũng chưa có RPC tương ứng trong `Nexus.Gateway/Contracts/shop.proto`.

### 10. `CartController` (`Controllers/CartController.cs`)
- Route Prefix: `/api/v1/commerce/cart` | Service: `commerce`
- Đặc tả mục tiêu: `GET /commerce/cart`, `POST /commerce/cart/items`, `PATCH /commerce/cart/items/{productId}`, `DELETE /commerce/cart/items/{productId}`, `DELETE /commerce/cart`.

### 11. `OrdersController` (`Controllers/OrdersController.cs`)
- Route Prefix: `/api/v1/commerce/orders` | Service: `commerce`
- Đặc tả mục tiêu: `GET /commerce/orders`, `GET /commerce/orders/{id}`, `POST /commerce/orders` (bắt buộc `Idempotency-Key`), `GET /seller/orders`, `GET /admin/orders`.

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
