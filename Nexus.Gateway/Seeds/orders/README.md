# Thư Mục Mock Seed Data Cho Tính Năng Orders (Commerce)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — dữ liệu seed tĩnh phục vụ Mock; không phải nguồn sự thật của nghiệp vụ.
> **Nguồn sự thật:** code trong repo này (`Seeds/` + `MockDataMapper`).
> **Cập nhật lần cuối:** 2026-10-24.

Thư mục này dành cho Team triển khai dữ liệu mock cho các API đơn hàng (`GET /api/v1/commerce/orders`, `POST /api/v1/commerce/orders`, v.v.).

### Cách thêm file mock JSON:
1. Tạo file JSON: `orders.json` hoặc `order.json`.
2. Định dạng JSON mẫu:
```json
{
  "items": [
    {
      "id": "ord-001",
      "orderNo": "ORD-20261023-0001",
      "status": "PendingPayment",
      "finalAmount": 1500000
    }
  ],
  "total": 1
}
```
3. Trong `OrdersController.cs`, gọi `_dispatcher.DispatchAsync(HttpContext, "orders", "orders", ...)`.
