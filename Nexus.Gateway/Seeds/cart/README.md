# Thư Mục Mock Seed Data Cho Tính Năng Cart (Commerce)

Thư mục này dành cho Team triển khai dữ liệu mock cho các API giỏ hàng (`GET /api/v1/commerce/cart`, `POST /api/v1/commerce/cart/items`, v.v.).

### Cách thêm file mock JSON:
1. Tạo file JSON: `cart.json`.
2. Định dạng JSON mẫu:
```json
{
  "userId": "11111111-1111-1111-1111-000000000004",
  "items": [
    {
      "productId": "prod-001",
      "quantity": 2,
      "price": 1500000
    }
  ],
  "totalAmount": 3000000
}
```
3. Trong `CartController.cs`, gọi `_dispatcher.DispatchAsync(HttpContext, "cart", "cart", ...)`
