# Hướng Dẫn Định Nghĩa File Mock Trong Nexus Gateway

Gateway hỗ trợ bạn phát triển và test độc lập với backend microservices thông qua 2 cơ chế:
1. **Dynamic Declarative Mock (`.mock.json` / `.def.json`)**: Định nghĩa bất kỳ API REST nào, hỗ trợ hot-reload, tự động hiện lên Swagger UI.
2. **Static Protobuf Fixtures (`Mocks/<service>/<operation>.json`)**: Mock dữ liệu cho các API Minimal đã khai báo trong code C#.

---

## 1. Dynamic Declarative Mock (*.mock.json hoặc *.def.json)

Đặt file ở bất kỳ đâu trong thư mục `Mocks/` (ví dụ: `Mocks/catalog/search.mock.json`).

### 1.1 Template Endpoint Đơn (responseType: 'single')
Gateway sẽ tự động bọc response vào vỏ chuẩn: `{ success: true, statusCode: 200, message: '...', data: { ... } }`.

```json
{
  "id": "get-product-detail",
  "service": "catalog",
  "method": "GET",
  "path": "/gateway/api/v1/catalog/products/{id}",
  "description": "Lấy chi tiết một sản phẩm",
  "policy": "public",
  "statusCode": 200,",
  "responseType": "single",
  "message": "Lấy thông tin sản phẩm thành công.",
  "response": {
    "id": "22222222-2222-2222-2222-000000000001",
    "name": "Bàn phím cơ RGB Custom",
    "price": "1250000.00",
    "currency": "VND",
    "stock": 50
  }
}
```

---

### 1.2 Template Phân Trang (responseType: 'paged')
Hỗ trợ tự động bọc dữ liệu phân trang và nhận query `?page=1&pageSize=20`:

```json
{
  "id": "search-products-paged",
  "service": "catalog",
  "method": "POST",
  "path": "/gateway/api/v1/catalog/search",
  "description": "Tìm kiếm sản phẩm theo từ khóa",
  "policy": "public",
  "statusCode": 200,
  "responseType": "paged",
  "request": {
    "keyword": "bàn phím",
    "minPrice": 500000
  },
  "response": {
    "items": [
      {
        "id": "22222222-2222-2222-2222-000000000001",
        "name": "Bàn phím cơ RGB Custom",
        "price": "1250000.00"
      }
    ],
    "total": 1
  }
}
```

---

### 1.3 Template Giả Lập Lỗi Nghiệp Vụ (responseType: 'error')
Gateway tự động trả về Problem Details chuẩn RFC 7807:

```json
{
  "id": "product-out-of-stock",
  "service": "catalog",
  "method": "POST",
  "path": "/gateway/api/v1/catalog/orders/quick-buy",
  "description": "Giả lập lỗi hết hàng trong kho",
  "policy": "authenticated",
  "statusCode": 422,
  "responseType": "error",
  "errorCode": "PRODUCT_OUT_OF_STOCK",
  "message": "Sản phẩm đã hết hàng trong kho."
}
```

---

### 1.4 Template Trả JSON Gốc Không Bọc Vỏ (responseType: 'raw')

```json
{
  "id": "raw-config-endpoint",
  "service": "catalog",
  "method": "GET",
  "path": "/gateway/api/v1/catalog/app-config",
  "description": "Trả JSON thô không qua bọc ApiResponse",
  "policy": "public",
  "statusCode": 200,
  "responseType": "raw",
  "response": {
    "version": "1.0.0",
    "maintenance": false
  }
}
```

---

### 1.5 Định Nghĩa Nhiều Endpoint Trong Một File (JSON Array)
Một file `.mock.json` có thể chứa 1 mảng `[ ... ]` các định nghĩa:

```json
[
  {
    "id": "catalog-brands",
    "service": "catalog",
    "method": "GET",
    "path": "/gateway/api/v1/catalog/brands",
    "policy": "public",
    "responseType": "single",
    "response": ["Keychron", "Akko", "Logitech"]
  },
  {
    "id": "catalog-tags",
    "service": "catalog",
    "method": "GET",
    "path": "/gateway/api/v1/catalog/tags",
    "policy": "public",
    "responseType": "single",
    "response": ["gaming", "office", "wireless"]
  }
]
```

---

### 1.6 Tách File Dữ Liệu Rời (responseFile / requestFile)
Khi dữ liệu mock dài, có thể tách sang file riêng cùng thư mục:

```json
{
  "id": "heavy-catalog-data",
  "service": "catalog",
  "method": "GET",
  "path": "/gateway/api/v1/catalog/large-inventory",
  "policy": "seller",
  "responseType": "single",
  "responseFile": "inventory-data.json"
}
```

---

## 2. Bảng Tham Chiếu Thuộc Tính

| Thuộc tính | Bắt buộc | Giá trị hợp lệ | Ghi chú |
|---|---|---|---|
| `id` | Có | Chuỗi không dấu, ví dụ: `"catalog-filter"` | Định danh duy nhất |
| `service` | Không | `"catalog"`, `"commerce"`, `"users"`, `"auction"`, `"fulfillment"`, `"notification"`, `"audit"` | Nếu bỏ trống, Gateway tự đoán qua URL |
| `method` | Có | `"GET"`, `"POST"`, `"PUT"`, `"DELETE"`, `"PATCH"` | Không phân biệt hoa/thường |
| `path` | Có | `"/gateway/api/v1/service/..."` | Hỗ trợ tham số: `{id}`, `{slug}`, `{id:guid}` |
| `policy` | Không | `"public"`, `"authenticated"`, `"buyer"`, `"seller"`, `"staff"`, `"admin"` | Mặc định: `"public"` |
| `statusCode` | Không | Số nguyên, ví dụ: `200`, `201`, `400`, `404`, `422` | Mặc định: `200` |
| `responseType` | Không | `"single"`, `"paged"`, `"raw"`, `"error"` | Mặc định: `"single"` |
| `message` | Không | Chuỗi thông điệp | Xuất hiện ở trường `message` trong response |
| `request` | Không | Object JSON | Dùng làm Body mẫu hiển thị trên Swagger UI |
| `response` | Không | Object hoặc Array JSON | Dữ liệu mock trả về |
| `responseFile` | Không | Tên file hoặc đường dẫn | Đọc response từ file json bên ngoài |

---

## 3. Cách Test Quyền Hạn (RBAC) Trên Swagger

1. **Khi Service ở chế độ Mock**:
   - Bạn chỉ cần bấm nút **Authorize** màu xanh góc trên Swagger.
   - Nhập một trong các từ khóa: `admin`, `seller`, `staff`, `buyer`.
   - Bấm **Authorize** -> Gọi API mock bình thường.
   - Nếu bật `"AuthMode": "Bypass"` trong `gateway.runtime.json`, bạn thậm chí **không cần đăng nhập** (bấm execute trực tiếp).
2. **Khi Service ở chế độ Real**:
   - Gateway **bắt buộc** phải có Token JWT thật ký bởi Identity Server.
   - Nếu nhập token giả (`admin`, chuỗi rác...), Gateway sẽ chặn ngay với lỗi **`401 Unauthorized`**.
