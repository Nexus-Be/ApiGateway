# Thư Mục Mock Seed Data Cho Tính Năng Products (Catalog)

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
