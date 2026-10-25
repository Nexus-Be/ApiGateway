# Hướng Dẫn Định Nghĩa Dữ Liệu Seed Fixtures & Mock API Trong Nexus Gateway
## (QUY CHUẨN MOCK DỮ LIỆU ĐỘC LẬP CHO TEAM PHÁT TRIỂN)

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — **9 file seed** thật (auth 4 + users 5); `products/`, `cart/`, `orders/` mới chỉ có `README.md` (placeholder, chưa có JSON). Song song tồn tại cơ chế mock khai báo `Mocks/*.mock.json` (6 file).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.

Thư mục `Seeds/` là nguồn dữ liệu giả lập chính cho các API Gateway phục vụ ở chế độ `Mock` (đọc bởi `Dispatching/MockDataMapper.cs`). Đây **không phải** cơ chế mock duy nhất — xem thêm `Nexus.Gateway/Mocks/README.md`.

---

## 1. Cấu Trúc Phân Tầng Theo Tính Năng (Feature-Based Hierarchy)

Dữ liệu seed được tổ chức theo từng nhóm tính năng nghiệp vụ rõ ràng:

```
Seeds/
├── auth/         <── [Đã có] 4 file: register.json, login.json, verify-otp.json, resend-otp.json
├── users/        <── [Đã có] 5 file: login.json, profile.json, register.json, resend-otp.json, verify-otp.json
├── products/     <── [Placeholder] chỉ có README.md — CHƯA có JSON
├── cart/         <── [Placeholder] chỉ có README.md — CHƯA có JSON
└── orders/       <── [Placeholder] chỉ có README.md — CHƯA có JSON
```

> ⚠️ **Đính chính**: bản cũ ghi `Seeds/users/` có "1 file seed: profile.json" — thực tế có **5 file** (kèm `login`, `register`, `resend-otp`, `verify-otp`). Trong khi đó `products/`, `cart/`, `orders/` **chưa có** file JSON nào nên không thể phục vụ mock cho `catalog`/`commerce`.

Mỗi file seed được copy ra thư mục output khi build nhờ `Nexus.Gateway.csproj` (`<Content Update="Seeds/**/*.json" CopyToOutputDirectory="PreserveNewest" ... />`).

---

## 2. Quy Trình 3 Bước Để Team Thêm 1 API Mock Mới

### 👉 Bước 1: Khai báo Endpoint trong Controller tương ứng
Controller hiện tại của nhóm Catalog là `Controllers/ProductsController.cs` (⚠️ **stub 10 dòng, 0 action**, route `api/v1/catalog/products`). Thêm action theo mẫu `AuthController`:

```csharp
[HttpGet("")]                                  // ghép với [Route("api/v1/catalog/products")]
[AllowAnonymous]
public Task<IResult> ListProducts()
{
    return _dispatcher.DispatchAsync(
        HttpContext,
        "catalog",                       // TÊN SERVICE CHUẨN (không phải "products")
        "products",                      // tên file JSON trong Seeds/catalog/
        o => _client.ListProductsAsync(new PageRequest(), o),
        ProductList.Parser);             // parser protobuf thật (BẮT BUỘC — xem cảnh báo dưới)
}
```

> ⚠️ **Ba điều bản cũ làm sai**:
> 1. Tham số thứ hai phải là **tên service chuẩn** trong `Gateway:Services`: `catalog`, `commerce`, `fulfillment`, `users`, `auction`, `notification`, `log`, `wallet` (`GatewayOptions.CanonicalServices` + `ResolveCanonicalName`). Truyền `"products"` sẽ khiến dispatcher trả **503 `SERVICE_CONFIG_MISSING`**.
> 2. **Không** truyền `null!` cho parser: nhánh `Real` cần `MessageParser<T>`, và seed JSON cũng được parse bằng đúng parser đó để validate schema.
> 3. Action **không** tự rẽ nhánh Mock/Real — luôn gọi `DispatchAsync`/`DispatchPagedAsync`.

### 👉 Bước 2: Tạo file JSON dữ liệu mẫu trong `Seeds/{service}/{operation}.json`
Tạo file `Seeds/catalog/products.json`:
```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-000000000001",
      "name": "Bàn phím cơ Bluetooth",
      "price": "1250000.00",
      "status": "Active"
    }
  ],
  "total": 1
}
```
> ⚠️ File này **chưa tồn tại**; cần tạo cùng lúc với message protobuf `ProductList` (hiện `Contracts/shop.proto` không có). Nếu message không tồn tại thì không có parser để validate và nhánh Mock không chạy được.

### 👉 Bước 3: Mở Swagger kiểm tra kết quả ngay!
1. Truy cập `http://localhost:8000/swagger`.
2. Bấm vào endpoint vừa tạo → **Try it out** → **Execute**.
3. Gateway bọc dữ liệu vào vỏ chuẩn:
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Products retrieved successfully.",
  "data": { },
  "traceId": "0HNPGSVMA0V0K",
  "timestamp": "2026-10-24T19:00:00Z"
}
```

> ⚡ **Lưu ý quan trọng**:
> - **Hot-Reload có thật**: `MockDataMapper` đọc file bằng `File.ReadAllTextAsync` **mỗi request** (không cache), nên sửa file trong `Seeds/` thì kết quả API đổi ngay, không cần restart Gateway.
> - Khi gọi API mock, các cơ chế biên như **Rate Limiting (Redis)**, **CORS**, **Tracing (`X-Trace-Id`)**, **Enum PascalCase** vẫn hoạt động như khi gọi backend thật.
> - Route **đã có action controller** luôn do controller xử lý; `Mocks/*.mock.json` chỉ phục vụ route **không** khớp action nào (`MockEngine/MockEngineMiddleware.cs:30-51`).
