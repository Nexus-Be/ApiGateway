# Thư Mục Mock Seed Data Cho Tính Năng Products (Catalog)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — dữ liệu seed tĩnh phục vụ Mock; không phải nguồn sự thật của nghiệp vụ.
> **Nguồn sự thật:** code trong repo này (`Seeds/` + `MockDataMapper`).
> **Cập nhật lần cuối:** 2026-10-24.

Thư mục này dành cho Team triển khai dữ liệu mock cho các API sản phẩm (`GET /api/v1/catalog/products`, `GET /api/v1/catalog/products/{id}`, v.v.).

### Cách thêm file mock JSON:
1. Tạo file JSON tương ứng với tên action, ví dụ: `products.json` hoặc `product-detail.json`.
2. Định dạng JSON mẫu:
```json
{
  "items": [
    {
      "id": "prod-001",
      "name": "Bàn phím cơ không dây",
      "price": 1500000,
      "currency": "VND",
      "status": "Active"
    }
  ],
  "total": 1
}
```
3. Trong Controller tương ứng (`ProductsController.cs`), gọi:
```csharp
[HttpGet("catalog/products")]
public Task<IResult> ListProducts()
{
    return _dispatcher.DispatchAsync(
        HttpContext, "products", "products", o => null!, null!, "Products retrieved successfully.");
}
```
4. Mở Swagger để kiểm tra kết quả ngay lập tức (không cần restart Gateway).
