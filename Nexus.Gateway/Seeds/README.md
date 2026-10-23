# Hướng Dẫn Định Nghĩa Dữ Liệu Seed Fixtures & Mock API Trong Nexus Gateway
## (QUY CHUẨN MOCK DỮ LIỆU ĐỘC LẬP CHO TEAM PHÁT TRIỂN)

Thư mục `Seeds/` là **nguồn dữ liệu giả lập duy nhất (Single Source of Mock Data)** cho toàn bộ hệ thống API Gateway.

---

## 1. Cấu Trúc Phân Tầng Theo Tính Năng (Feature-Based Hierarchy)

Dữ liệu seed được tổ chức theo từng nhóm tính năng nghiệp vụ rõ ràng:

```
Seeds/
├── auth/         <── [Đã hoàn thiện] 4 file seed: register.json, login.json, verify-otp.json, resend-otp.json
├── users/        <── [Đã hoàn thiện] 1 file seed: profile.json (dành cho API GET /users/me)
├── products/     <── [Chờ team thêm] Danh mục & Sản phẩm (products.json, product.json...)
├── cart/         <── [Chờ team thêm] Giỏ hàng mua sắm (cart.json...)
└── orders/       <── [Chờ team thêm] Đơn hàng (orders.json, order.json...)
```

---

## 2. Quy Trình 3 Bước Để Team Thêm 1 API Mock Mới (Cực Kỳ Đơn Giản)

### 👉 Bước 1: Khai báo Endpoint trong Controller tương ứng
Vào file Controller rỗng đã chuẩn bị sẵn (ví dụ: `ProductsController.cs`):
```csharp
[HttpGet("catalog/products")]
[AllowAnonymous]
public Task<IResult> ListProducts()
{
    return _dispatcher.DispatchAsync(
        HttpContext,
        "products",   // Tên thư mục hoặc service trong Seeds/
        "products",   // Tên file JSON trong Seeds/ (không cần đuôi .json)
        o => null!,   // Callback gRPC thật (truyền null khi đang làm mock)
        null!,        // Parser Protobuf (truyền null khi chưa có proto)
        successMessage: "Products retrieved successfully."
    );
}
```

### 👉 Bước 2: Tạo file JSON dữ liệu mẫu trong `Seeds/{tính_năng}/`
Tạo file `Seeds/products/products.json`:
```json
{
  "items": [
    {
      "id": "prod-001",
      "name": "Bàn phím cơ Bluetooth",
      "price": 1250000,
      "status": "Active"
    }
  ],
  "total": 1
}
```

### 👉 Bước 3: Mở Swagger kiểm tra kết quả ngay!
1. Truy cập `http://localhost:8000/swagger`.
2. Bấm vào endpoint vừa tạo $\rightarrow$ **Try it out** $\rightarrow$ **Execute**.
3. Gateway sẽ tự động bọc dữ liệu của bạn vào vỏ bọc chuẩn hệ thống:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Products retrieved successfully.",
  "data": { ... },
  "traceId": "0HNPGSVMA0V0K",
  "timestamp": "2026-10-23T19:00:00Z"
}
```

> ⚡ **Lưu ý quan trọng**:
> - Gateway hỗ trợ **Hot-Reload file JSON tức thì**: bạn sửa file trong `Seeds/` thì kết quả API đổi ngay lập tức, không cần tắt/bật lại Gateway.
> - Khi gọi API mock, mọi cơ chế an ninh như **Rate Limiting (Redis)**, **CORS**, **Tracing (X-Trace-Id)**, **Chuẩn hóa Enum PascalCase** đều tự động hoạt động đồng nhất như khi gọi backend thật.
