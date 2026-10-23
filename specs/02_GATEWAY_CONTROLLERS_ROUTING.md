# 02 — BẢN ĐẶC TẢ CONTROLLERS & ĐỊNH TUYẾN (CONTROLLERS & ROUTING SPEC)

Toàn bộ các API được quản lý bởi 15 Controllers tiêu chuẩn `[ApiController] ControllerBase` phân bổ phẳng theo từng nghiệp vụ/tài nguyên độc lập (Single Responsibility Principle) với tiền tố chuẩn hóa:
$$\mathbf{/api/v1/\{resource\}/...}$$

---

## 1. DANH MỤC 15 CONTROLLERS & CÁC ROUTE

### 1. `AuthController` (`Controllers/AuthController.cs`)
- Route Prefix: `/api/v1/auth` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `POST /register` [AllowAnonymous] $\rightarrow$ Đăng ký tài khoản mới (`RegisterRequestDto`).
  - `POST /verify-otp` [AllowAnonymous] $\rightarrow$ Xác thực tài khoản bằng mã OTP (`VerifyOtpRequestDto`).
  - `POST /resend-otp` [AllowAnonymous] $\rightarrow$ Gửi lại mã OTP (`ResendOtpRequestDto`).
  - `POST /login` [AllowAnonymous] $\rightarrow$ Đăng nhập lấy access token (`LoginRequestDto`).

### 2. `UsersController` (`Controllers/UsersController.cs`)
- Route Prefix: `/api/v1` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /users/me` [Authorize("authenticated")] $\rightarrow$ Lấy thông tin cá nhân.
  - `PUT /users/me` [Authorize("authenticated")] $\rightarrow$ Cập nhật hồ sơ (`UpdateProfileRequestDto`).
  - `POST /users/me/seller-upgrade` [Authorize("authenticated")] $\rightarrow$ Nộp đơn đăng ký Seller (`SellerUpgradeRequestDto`).

### 3. `AdminUsersController` (`Controllers/AdminUsersController.cs`)
- Route Prefix: `/api/v1` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /admin/users` [Authorize("staff")] $\rightarrow$ Danh sách user phân trang.
  - `POST /admin/sellers/{id}/approve` [Authorize("staff")] $\rightarrow$ Duyệt/từ chối hồ sơ seller (`ApproveSellerRequestDto`).
  - `POST /admin/users` [Authorize("admin")] $\rightarrow$ Quản trị viên tạo user mới (`CreateUserRequestDto`).
  - `POST /admin/users/{id}/roles` [Authorize("admin")] $\rightarrow$ Gán roles cho user (`AssignRolesRequestDto`).
  - `GET /admin/roles` [Authorize("admin")] $\rightarrow$ Lấy danh mục roles.
  - `GET /admin/permissions` [Authorize("admin")] $\rightarrow$ Lấy danh mục permissions.

### 4. `CartController` (`Controllers/CartController.cs`)
- Route Prefix: `/api/v1` | Service: `commerce`
- Attributes: `[ApiController]`, `[BackendTag("commerce")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /commerce/cart` (alias `/cart`) [Authorize("authenticated")] $\rightarrow$ Lấy giỏ hàng của user.
  - `POST /commerce/cart/items` (alias `/cart/items`) [Authorize("authenticated")] $\rightarrow$ Thêm sản phẩm vào giỏ (`AddCartItemRequestDto`).
  - `PATCH /commerce/cart/items/{productId}` (alias `/cart/items/{productId}`) [Authorize("authenticated")] $\rightarrow$ Sửa số lượng sản phẩm (`UpdateCartItemRequestDto`).
  - `DELETE /commerce/cart/items/{productId}` (alias `/cart/items/{productId}`) [Authorize("authenticated")] $\rightarrow$ Xóa sản phẩm khỏi giỏ.
  - `DELETE /commerce/cart` (alias `/cart`) [Authorize("authenticated")] $\rightarrow$ Xóa sạch giỏ hàng.

### 5. `OrdersController` (`Controllers/OrdersController.cs`)
- Route Prefix: `/api/v1` | Service: `commerce`
- Attributes: `[ApiController]`, `[BackendTag("commerce")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /commerce/orders` (alias `/orders`) [Authorize("authenticated")] $\rightarrow$ Danh sách đơn hàng cá nhân.
  - `GET /commerce/orders/{id}` (alias `/orders/{id}`) [Authorize("authenticated")] $\rightarrow$ Chi tiết đơn hàng theo UUID.
  - `POST /commerce/orders` (alias `/orders`) [Authorize("authenticated")] $\rightarrow$ Đặt hàng checkout (`CheckoutRequestDto`, bắt buộc `Idempotency-Key`).
  - `GET /seller/orders` [Authorize("seller")] $\rightarrow$ Đơn hàng của shop.
  - `GET /admin/orders` [Authorize("staff")] $\rightarrow$ Toàn bộ đơn hàng trên sàn.

### 6. `ProductsController` (`Controllers/ProductsController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /catalog/products` (alias `/products`) [AllowAnonymous] $\rightarrow$ Danh sách sản phẩm phân trang, tìm kiếm.
  - `GET /catalog/products/{id}` (alias `/products/{id}`) [AllowAnonymous] $\rightarrow$ Chi tiết sản phẩm theo UUID.
  - `GET /seller/products` [Authorize("seller")] $\rightarrow$ Danh sách sản phẩm của người bán.
  - `POST /seller/products` [Authorize("seller")] $\rightarrow$ Đăng bán sản phẩm (`ProductRequestDto`).
  - `PUT /seller/products/{id}` [Authorize("seller")] $\rightarrow$ Sửa thông tin sản phẩm.
  - `DELETE /seller/products/{id}` [Authorize("seller")] $\rightarrow$ Xóa sản phẩm.

### 7. `CategoriesController` (`Controllers/CategoriesController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /catalog/categories` (alias `/categories`) [AllowAnonymous] $\rightarrow$ Danh mục sản phẩm phân trang.
  - `POST /catalog/categories` (alias `/categories`) [Authorize("admin")] $\rightarrow$ Tạo danh mục mới (`CategoryRequestDto`).
  - `PUT /catalog/categories/{id}` (alias `/categories/{id}`) [Authorize("admin")] $\rightarrow$ Cập nhật danh mục.
  - `DELETE /catalog/categories/{id}` (alias `/categories/{id}`) [Authorize("admin")] $\rightarrow$ Xóa danh mục.

### 8. `InventoryController` (`Controllers/InventoryController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /seller/inventory` [Authorize("seller")] $\rightarrow$ Xem kho ảo của seller.
  - `PUT /seller/inventory/{productId}` [Authorize("seller")] $\rightarrow$ Điều chỉnh tồn kho ảo (`AdjustInventoryRequestDto`).

### 9. `AuctionController` (`Controllers/AuctionController.cs`)
- Route Prefix: `/api/v1/auctions` | Service: `auction`
- Attributes: `[ApiController]`, `[BackendTag("auction")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /` [AllowAnonymous] $\rightarrow$ Danh sách phiên đấu giá phân trang.
  - `GET /{id}` [AllowAnonymous] $\rightarrow$ Chi tiết phiên đấu giá.
  - `POST /` [Authorize("seller")] $\rightarrow$ Tạo phiên đấu giá (`CreateAuctionRequestDto`).
  - `POST /{id}/bids` [Authorize("authenticated")] $\rightarrow$ Đặt giá bid (`PlaceBidRequestDto`).
  - `GET /{id}/bids` [AllowAnonymous] $\rightarrow$ Lịch sử các lượt đặt giá.

### 10. `FulfillmentController` (`Controllers/FulfillmentController.cs`)
- Route Prefix: `/api/v1/fulfillment` | Service: `fulfillment`
- Attributes: `[ApiController]`, `[BackendTag("fulfillment")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /inventory/{skuId}` [AllowAnonymous] $\rightarrow$ Tồn kho vật lý kho bãi.
  - `POST /shipping/quote` [AllowAnonymous] $\rightarrow$ Tính phí giao hàng (`ShippingQuoteRequestDto`).
  - `GET /shipments/{trackingCode}` [AllowAnonymous] $\rightarrow$ Tra cứu vận đơn.

### 11. `NotificationController` (`Controllers/NotificationController.cs`)
- Route Prefix: `/api/v1/notifications` | Service: `notification`
- Attributes: `[ApiController]`, `[BackendTag("notification")]`, `[EnableRateLimiting("api")]`, `[Authorize("authenticated")]`
- Actions:
  - `GET /` $\rightarrow$ Danh sách thông báo cá nhân.
  - `PUT /{id}/read` $\rightarrow$ Đánh dấu thông báo đã đọc.
  - `GET /preferences` $\rightarrow$ Lấy cài đặt nhận thông báo.
  - `PUT /preferences` $\rightarrow$ Cập nhật cài đặt (`UpdatePreferencesRequestDto`).

### 12. `LogController` (`Controllers/LogController.cs`)
- Route Prefix: `/api/v1/log` | Service: `log`
- Attributes: `[ApiController]`, `[BackendTag("log")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /logs` [Authorize("staff")] $\rightarrow$ Tra cứu nhật ký hệ thống.
  - `GET /logs/{id}` [Authorize("staff")] $\rightarrow$ Chi tiết 1 bản ghi log.
  - `GET /event-types` [Authorize("staff")] $\rightarrow$ Danh mục các loại sự kiện log.
  - `PUT /event-types/{code}` [Authorize("admin")] $\rightarrow$ Bật/tắt loại sự kiện (`LogEventTypeToggleRequestDto`).

### 13. `WalletController` (`Controllers/WalletController.cs`)
- Route Prefix: `/api/v1/wallet` | Service: `wallet`
- Attributes: `[ApiController]`, `[BackendTag("wallet")]`, `[EnableRateLimiting("api")]`, `[Authorize("authenticated")]`
- Actions:
  - `GET /balance` $\rightarrow$ Xem số dư ví hiện tại.
  - `POST /deposit` $\rightarrow$ Nạp tiền vào ví (`WalletDepositRequestDto`).
  - `POST /withdraw` $\rightarrow$ Rút tiền về tài khoản ngân hàng (`WalletWithdrawRequestDto`).
  - `POST /transfer` $\rightarrow$ Chuyển tiền giữa các ví (`WalletTransferRequestDto`).
  - `GET /transactions` $\rightarrow$ Lịch sử giao dịch ví phân trang.

### 14. `GatewayControlController` (`Controllers/GatewayControlController.cs`)
- Route Prefix: `/api/v1` | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/modes` $\rightarrow$ Lấy trạng thái AuthMode và trạng thái Mock/Real của 8 services.
  - `PUT /gateway/mode` $\rightarrow$ Chuyển đổi AuthMode (`"Standard"` vs `"Bypass"`).
  - `PUT /gateway/modes/{service}` $\rightarrow$ Chuyển đổi chế độ Mock/Real cho 1 service.
  - `PUT /gateway/modes` $\rightarrow$ Cập nhật hàng loạt chế độ các service.
  - `GET /mock-engine/definitions` $\rightarrow$ Xem danh sách declarative mock endpoints động.
  - `POST /mock-engine/reload` $\rightarrow$ Kích hoạt quét và nạp lại mock definitions nóng.

### 15. `HealthProberController` (`Controllers/HealthProberController.cs`)
- Route Prefix: `/api/v1/gateway/prober` | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /status` $\rightarrow$ Lấy trạng thái prober, failure count, latency, recent samples của toàn bộ 8 services.
  - `GET /status/{service}` $\rightarrow$ Chi tiết trạng thái prober của 1 service cụ thể.
  - `PUT /{service}` $\rightarrow$ Bật/tắt prober daemon cho 1 service (`ServiceProberToggleDto`).
  - `PUT /bulk` $\rightarrow$ Bật/tắt hàng loạt prober daemons (`BulkProberToggleDto`).
  - `POST /{service}/check` $\rightarrow$ **Kiểm tra sức khỏe thủ công tức thì (Manual Check on demand)** — probe ngay 1 lần và trả về kết quả latency/status code.
