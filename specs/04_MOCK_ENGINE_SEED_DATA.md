# 04 — DỮ LIỆU SEED & DECLARATIVE MOCK ENGINE (MOCK & SEED SPEC)

---

## 1. DỮ LIỆU SEED CỦA 8 SERVICES (`Seeds/`)

Toàn bộ dữ liệu fixtures mẫu được lưu trữ tại `Nexus.Gateway/Seeds/{service}/{operation}.json`:
- `Seeds/users/`: `login.json`, `register.json`, `profile.json`, `roles.json`, `permissions.json`...
- `Seeds/catalog/`: `categories.json`, `products.json`, `product.json`, `inventory.json`...
- `Seeds/commerce/`: `cart.json`, `orders.json`, `order.json`, `order-created.json`...
- `Seeds/auction/`: `auctions.json`, `auction.json`, `bids.json`...
- `Seeds/fulfillment/`: `inventory.json`, `shipping-quote.json`, `shipment-tracking.json`...
- `Seeds/notification/`: `notifications.json`, `preferences.json`...
- `Seeds/audit/`: `audit-logs.json`, `audit-log.json`, `audit-event-types.json`...
- `Seeds/wallet/`: `wallet-balance.json`, `wallet-deposit.json`, `wallet-transactions.json`...

### Cơ Chế Thẩm Định Schema Qua Protobuf:
Khi một mock request được gọi, `MockDataMapper` đọc file JSON và nạp vào:
```csharp
var protoMessage = parser.ParseJson(json);
```
- Nếu JSON thiếu trường bắt buộc hoặc sai kiểu dữ liệu (ví dụ string thay vì int, thiếu trường định nghĩa trong `.proto`) $\rightarrow$ Parser sẽ ném ngoại lệ ngay lập tức, ngăn ngừa hoàn toàn việc trả dữ liệu sai lệch cho Frontend.

---

## 2. DECLARATIVE MOCK ENGINE (`*.mock.json`)

Dành cho các mock API động độc lập không cần viết code C#:
- File đặt tại `Nexus.Gateway/Seeds/declarative/*.mock.json` hoặc `Nexus.Gateway/Mocks/declarative/*.mock.json`.
- Tự động nạp khi khởi động và tự động quét hot-reload khi dev sửa file JSON.
- Tự động sinh tài liệu Swagger UI qua [MockEngineDocumentFilter.cs](../Nexus.Gateway/MockEngine/MockEngineDocumentFilter.cs).
- Hỗ trợ tham số URL động (dynamic route params) như `/api/v1/catalog/products/{id}`.
