# 02 — BẢN ĐẶC TẢ CONTROLLERS & ĐỊNH TUYẾN (CONTROLLERS & ROUTING SPEC)
> **Trạng thái triển khai (2026-10-25):** ⚠️ Một phần — 15/15 file controller tồn tại, 7/15 controller có action (43 action: 16 action cơ bản + 27 action catalog F2); 8 controller stub 10 dòng, 0 action.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-25.

Toàn bộ các API được quản lý bởi 15 file controller `[ApiController] ControllerBase` trong `Nexus.Gateway/Controllers/`, phân bổ phẳng theo từng nghiệp vụ/tài nguyên với tiền tố chuẩn hóa:
$$\mathbf{/api/v1/...}$$

Đếm thật: `grep -c '\[Http'` trên từng file cho ra **43 attribute action**, nằm trong 7 file: `AuthController.cs` (4), `UsersController.cs` (1), `GatewayControlController.cs` (6), `HealthProberController.cs` (5), `ProductsController.cs` (20), `CategoriesController.cs` (5), `InventoryController.cs` (2). 8 file còn lại chỉ có `[ApiController]` + `[Route(...)]` + comment `// TODO (Team)` — **không có action nào**, nên request tới các route đó rơi xuống declarative mock (nếu có `Mocks/*.mock.json` khớp) hoặc trả **404**.

---

## 1. BẢNG TỔNG HỢP 15 CONTROLLER

| # | Controller | Route Prefix (thật) | Số action | Trạng thái | Bằng chứng |
|:--:|:---|:---|:--:|:---|:---|
| 1 | `AuthController` | `/api/v1/auth` | 4 | ✅ CÓ | `Controllers/AuthController.cs` |
| 2 | `UsersController` | `/api/v1` | 1 | ✅ CÓ | `Controllers/UsersController.cs` |
| 3 | `GatewayControlController` | `/api/v1` | 6 | ✅ CÓ | `Controllers/GatewayControlController.cs` |
| 4 | `HealthProberController` | `/api/v1` | 5 | ✅ CÓ | `Controllers/HealthProberController.cs` |
| 5 | `ProductsController` | `/api/v1` | 20 | ✅ CÓ | `Controllers/ProductsController.cs` |
| 6 | `CategoriesController` | `/api/v1` | 5 | ✅ CÓ | `Controllers/CategoriesController.cs` |
| 7 | `InventoryController` | `/api/v1` | 2 | ✅ CÓ | `Controllers/InventoryController.cs` |
| 8 | `AdminUsersController` | `/api/v1/admin/users` | 0 | ❌ CHƯA CÓ | `Controllers/AdminUsersController.cs` (10 dòng, TODO) |
| 9 | `CartController` | `/api/v1/commerce/cart` | 0 | ❌ CHƯA CÓ | `Controllers/CartController.cs` (10 dòng, TODO) |
| 10 | `OrdersController` | `/api/v1/commerce/orders` | 0 | ❌ CHƯA CÓ | `Controllers/OrdersController.cs` (10 dòng, TODO) |
| 11 | `AuctionController` | `/api/v1/auction` | 0 | ❌ CHƯA CÓ | `Controllers/AuctionController.cs` (10 dòng, TODO) |
| 12 | `FulfillmentController` | `/api/v1/fulfillment` | 0 | ❌ CHƯA CÓ | `Controllers/FulfillmentController.cs` (10 dòng, TODO) |
| 13 | `NotificationController` | `/api/v1/notification` | 0 | ❌ CHƯA CÓ | `Controllers/NotificationController.cs` (10 dòng, TODO) |
| 14 | `LogController` | `/api/v1/log` | 0 | ❌ CHƯA CÓ | `Controllers/LogController.cs` (10 dòng, TODO) |
| 15 | `WalletController` | `/api/v1/wallet` | 0 | ❌ CHƯA CÓ | `Controllers/WalletController.cs` (10 dòng, TODO) |

---

## 2. CHI TIẾT 7 CONTROLLER ĐÃ CÓ ACTION

### 1. `AuthController` (`Controllers/AuthController.cs`)
- Route Prefix: `/api/v1/auth` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `POST /register` [AllowAnonymous] $\rightarrow$ Đăng ký tài khoản mới (`RegisterRequestDto`).
  - `POST /verify-otp` [AllowAnonymous] $\rightarrow$ Xác thực tài khoản bằng mã OTP (`VerifyOtpRequestDto`).
  - `POST /resend-otp` [AllowAnonymous] $\rightarrow$ Gửi lại mã OTP (`ResendOtpRequestDto`).
  - `POST /login` [AllowAnonymous] $\rightarrow$ Đăng nhập lấy access token (`LoginRequestDto`); gọi `UsersApi.UsersApiClient.LoginAsync` — phía Gateway đã có, phía User service chưa implement rpc `Login`.

### 2. `UsersController` (`Controllers/UsersController.cs`)
- Route Prefix: `/api/v1` | Service: `users`
- Attributes: `[ApiController]`, `[BackendTag("users")]`, `[EnableRateLimiting("api")]`
- Actions:
  - `GET /users/me` [Authorize("authenticated")] $\rightarrow$ Lấy thông tin cá nhân.
  - `PUT /users/me` và `POST /users/me/seller-upgrade` $\rightarrow$ ❌ CHƯA CÓ (đặc tả mục tiêu, chưa triển khai).

### 3. `GatewayControlController` (`Controllers/GatewayControlController.cs`)
- Route Prefix: `/api/v1` | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/modes` $\rightarrow$ Lấy `AuthMode` và trạng thái Mock/Real của 8 services.
  - `PUT /gateway/mode` $\rightarrow$ Chuyển `AuthMode` (`Enforce`/`Standard` vs `Bypass`; `Bypass` chỉ hợp lệ ở Development — body `AuthModeUpdateRequestDto`).
  - `PUT /gateway/modes/{service}` $\rightarrow$ Chuyển chế độ Mock/Real cho 1 service (`ServiceModeUpdateRequestDto`).
  - `PUT /gateway/modes` $\rightarrow$ Cập nhật hàng loạt chế độ các service (`BulkServiceModeUpdateRequestDto`).
  - `GET /mock-engine/definitions` $\rightarrow$ Xem danh sách declarative mock endpoints.
  - `POST /mock-engine/reload` $\rightarrow$ Quét và nạp lại mock definitions nóng.

### 4. `HealthProberController` (`Controllers/HealthProberController.cs`)
- Route Prefix: `/api/v1` (route con `gateway/prober/...`) | Service: *Nội bộ Gateway*
- Attributes: `[ApiController]`, `[EnableRateLimiting("api")]`, `[Authorize(GatewayPolicies.GatewayAdmin)]`, `[DisableMockAuthorizationBypass]`
- Actions:
  - `GET /gateway/prober/status` $\rightarrow$ Trạng thái prober, failure count, latency, recent samples của 8 services.
  - `GET /gateway/prober/status/{service}` $\rightarrow$ Chi tiết trạng thái prober của 1 service.
  - `PUT /gateway/prober/{service}` $\rightarrow$ Bật/tắt prober daemon cho 1 service (`ServiceProberToggleDto`).
  - `PUT /gateway/prober/bulk` $\rightarrow$ Bật/tắt hàng loạt prober daemons (`BulkProberToggleDto`).
  - `POST /gateway/prober/{service}/check` $\rightarrow$ **Kiểm tra sức khỏe thủ công tức thì** — probe ngay 1 lần và trả về latency/status code.

### 5. `ProductsController` (`Controllers/ProductsController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (20 routes khớp hợp đồng F2 quản lý sản phẩm):
  | Phương thức & Tuyến đường | Operation / Seed | Quyền hạn | Ghi chú |
  |:---|:---|:---|:---|
  | `GET /products` | `products` | `AllowAnonymous` | Phân trang: `page`, `pageSize`, `categoryId` |
  | `GET /product/search` | `product-search` | `Seller` | Phân trang + lọc đa tiêu chí |
  | `GET /product/{productId}` | `product` | `AllowAnonymous` | Chi tiết sản phẩm + variants + images + rating |
  | `POST /products` | `product-create` (201) | `Seller` | Tạo sản phẩm mới |
  | `PATCH /product/{productId}` | `product-update` | `Seller` | Cập nhật thông tin sản phẩm |
  | `DELETE /product/{productId}` | `product-delete` (204) | `Seller` | Xóa sản phẩm (NoContent) |
  | `POST /product/{productId}pics` | `product-images-add` (201) | `Seller` | Multipart form upload ảnh |
  | `GET /product/{productId}pics` | `product-images` | `AllowAnonymous` | Danh sách ảnh của sản phẩm |
  | `DELETE /product/{productId}pics?ids=` | `product-images-delete` (204) | `Seller` | Xóa danh sách ảnh (NoContent) |
  | `GET /products/{productId}/totalRating` | `product-rating-total` | `AllowAnonymous` | Tổng điểm đánh giá |
  | `GET /products/{productId}/rating` | `product-rating` | `AllowAnonymous` | Chi tiết đánh giá và phân trang |
  | `PATCH /product/{productId}/status` | `product-status` | `Seller` | Đổi trạng thái (`DRAFT`, `ACTIVE`, `INACTIVE`) |
  | `POST /product/{productId}/variants` | `variant-create` (201) | `Seller` | Thêm biến thể mới |
  | `DELETE /variant/{variantId}` | `variant-delete` (204) | `Seller` | Xóa biến thể (NoContent) |
  | `PATCH /variant/{variantId}` | `variant-update` | `Seller` | Cập nhật biến thể |
  | `GET /variant/{variantId}` | `variant` | `Seller` | Chi tiết biến thể |
  | `POST /product/auction` | `auction-product-create` (201) | `Seller` | Tạo sản phẩm đấu giá (SaleType AUCTION) |
  | `GET /product/auction/search` | `auction-product-search` | `Seller` | Tìm kiếm sản phẩm đấu giá (paged) |
  | `GET /product/auctions` | `auction-products` | `Seller` | Danh sách sản phẩm đấu giá (paged) |
  | `GET /product/auction/{auctionId}` | `auction-product` | `Seller` | Chi tiết sản phẩm đấu giá |

### 6. `CategoriesController` (`Controllers/CategoriesController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (5 routes danh mục F2):
  | Phương thức & Tuyến đường | Operation / Seed | Quyền hạn | Ghi chú |
  |:---|:---|:---|:---|
  | `GET /categories` | `categories` | `AllowAnonymous` | Lấy danh sách cây danh mục (`tree=true`) |
  | `GET /categories/{categoryId}` | `category` | `AllowAnonymous` | Chi tiết danh mục kèm cha và con |
  | `POST /categories` | `category-create` (201) | `Admin` | Tạo danh mục mới |
  | `PUT /categories/{categoryId}` | `category-update` | `Admin` | Cập nhật danh mục |
  | `DELETE /categories/{categoryId}` | `category-delete` (204) | `Admin` | Xóa danh mục (NoContent) |

### 7. `InventoryController` (`Controllers/InventoryController.cs`)
- Route Prefix: `/api/v1` | Service: `catalog`
- Attributes: `[ApiController]`, `[BackendTag("catalog")]`, `[EnableRateLimiting("api")]`
- Actions (2 routes kho F2):
  | Phương thức & Tuyến đường | Operation / Seed | Quyền hạn | Ghi chú |
  |:---|:---|:---|:---|
  | `POST /inventory/stock-in` | `inventory-stock-in` | `Seller` | Nhập kho nhiều SKU |
  | `POST /variant/{variantId}/stock-in` | `variant-stock-in` | `Seller` | Nhập kho 1 biến thể |

- Validate: `Common/CatalogValidation.cs`
- Test: `Nexus.Gateway.Tests/CatalogMockTests.cs` (42 test cases) + `CatalogSeedConformanceTests.cs` (27 seed conformance cases)

---

## 3. ĐẶC TẢ MỤC TIÊU CHO 8 CONTROLLER STUB (CHƯA TRIỂN KHAI)
Các route dưới đây là **đặc tả mục tiêu, chưa triển khai** — không controller nào trong danh sách này có action, và hầu hết cũng chưa có RPC tương ứng trong `Nexus.Gateway/Contracts/shop.proto`.

### 8. `AdminUsersController` (`Controllers/AdminUsersController.cs`)
- Route Prefix: `/api/v1/admin/users` | Service: `users`
- Đặc tả mục tiêu: `GET /admin/users` [staff], `POST /admin/sellers/{id}/approve` [staff], `POST /admin/users` [admin], `POST /admin/users/{id}/roles` [admin], `GET /admin/roles` [admin], `GET /admin/permissions` [admin].

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
