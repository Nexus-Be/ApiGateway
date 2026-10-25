# Hướng Dẫn Sử Dụng & Định Nghĩa Mock API Tại Nexus Gateway

> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — dữ liệu seed tĩnh phục vụ Mock; không phải nguồn sự thật của nghiệp vụ.
> **Nguồn sự thật:** code trong repo này (`Seeds/` + `MockDataMapper`).
> **Cập nhật lần cuối:** 2026-10-24.

> ⚠️ **Lưu Ý Kiến Trúc Quan Trọng**:
> Thư mục `Mocks/` này chứa các định nghĩa Declarative Mock cấp thấp được nạp bởi `MockEngineMiddleware`. 
> Quy chuẩn phát triển API chuẩn **DUY NHẤT** của hệ thống Nexus Gateway là **Khai báo Endpoint trong Controller C# + Tạo dữ liệu Seed Fixture trong thư mục `Seeds/`** (xem chi tiết tại [MOCK_GUIDE.md](../../MOCK_GUIDE.md)).
> Khi một Controller Action C# đã được khai báo, Gateway sẽ **tự động ưu tiên Controller** và bỏ qua định nghĩa trong thư mục `Mocks/` này.

Gateway cung cấp cơ chế **Dynamic Declarative Mock (`*.mock.json`)** giúp Frontend và Backend team có thể phát triển, tích hợp và kiểm thử giao diện hoàn toàn độc lập mà không cần chờ backend service chạy hay viết bất kỳ dòng code C# nào.

---

## 1. Cơ Chế Hoạt Động & Quy Chuẩn Response Factory

Khi một request được gửi đến Gateway:
1. `MockEngineMiddleware` quét các file `.mock.json` trong thư mục `Mocks/`.
2. Nếu khớp Method và Route Path (và service tương ứng đang ở chế độ `Mock`), Gateway sẽ lập tức trả về dữ liệu mock.
3. **Chuẩn hóa qua `ApiResponseFactory`**: Mọi response dạng `single` sẽ được bọc tự động trong cấu trúc chuẩn:
   ```json
   {
     "success": true,
     "statusCode": 200,
     "message": "Sentence case success message in 100% English.",
     "data": { ... },
     "traceId": "0HNPGSVMA0V0K",
     "timestamp": "2026-10-23T17:00:00Z"
   }
   ```
4. **Tự động hiện trên Swagger UI**: Tất cả các file `.mock.json` được tự động nạp vào Swagger UI (`/swagger`), hỗ trợ đầy đủ Request Body mẫu và Execute trực tiếp.

---

## 2. 5 File Mock Mẫu Chuẩn Cho Team Tham Khảo (Reference Blueprints)

Trong thư mục `Mocks/` này có sẵn **5 Mock API chuẩn mẫu** đại diện cho các trường hợp phổ biến:

| File | Method & Path | Policy | Response Type | Mục Đích Tham Khảo |
| :--- | :--- | :---: | :---: | :--- |
| **`auth-register.mock.json`** | `POST /api/v1/auth/register` | `public` | `single` | Mẫu API đăng ký công khai, nhận Request body, trả về `UserProfile` với status `PendingVerification`. |
| **`auth-resend-otp.mock.json`** | `POST /api/v1/auth/resend-otp` | `public` | `single` | Mẫu API gửi lại mã OTP, trả về `OtpResponse` (`success`, `message`, `email`). |
| **`auth-verify-otp.mock.json`** | `POST /api/v1/auth/verify-otp` | `public` | `single` | Mẫu API xác thực OTP, chuyển status của `UserProfile` sang `Active`. |
| **`auth-login.mock.json`** | `POST /api/v1/auth/login` | `public` | `single` | Mẫu API đăng nhập, trả về `LoginResponse` (`accessToken`, `expiresAt`, `userId`, `roles`). |
| **`users-me.mock.json`** | `GET /api/v1/users/me` | `authenticated` | `single` | Mẫu API yêu cầu xác thực người dùng (`Bearer Token`), trả về `UserProfile` cá nhân. |

---

## 3. Cấu Trúc Chi Tiết Của Một File `.mock.json`

```json
{
  "id": "auth-register",
  "service": "users",
  "method": "POST",
  "path": "/api/v1/auth/register",
  "description": "Register a new user account with pending verification",
  "policy": "public",
  "statusCode": 200,
  "responseType": "single",
  "message": "User registered successfully.",
  "request": {
    "username": "johndoe",
    "email": "john.doe@example.com",
    "password": "Password123!",
    "displayName": "John Doe",
    "firstName": "John",
    "lastName": "Doe",
    "phoneNumber": "+84901234567"
  },
  "response": {
    "id": "11111111-1111-1111-1111-000000000099",
    "displayName": "John Doe",
    "email": "john.doe@example.com",
    "roles": ["buyer"],
    "status": "PendingVerification",
    "shopName": ""
  }
}
```

### Bảng Tra Cứu Các Thuộc Tính

| Thuộc tính | Bắt buộc | Giá trị hợp lệ | Ý nghĩa |
| :--- | :---: | :--- | :--- |
| **`id`** | Có | Chuỗi định danh kebab-case, ví dụ: `"auth-register"` | Định danh duy nhất của mock endpoint. |
| **`service`** | Có | `"users"`, `"catalog"`, `"commerce"`, `"auction"`, `"fulfillment"`, `"notification"`, `"wallet"`, `"log"` | Service phụ trách quản lý endpoint. |
| **`method`** | Có | `"GET"`, `"POST"`, `"PUT"`, `"PATCH"`, `"DELETE"` | Phương thức HTTP của API. |
| **`path`** | Có | `"/api/v1/..."` (hỗ trợ route param như `{id}`) | Đường dẫn URL của API. |
| **`description`** | Không | Chuỗi mô tả ngắn bằng tiếng Anh | Hiển thị tóm tắt trên Swagger UI. |
| **`policy`** | Không | `"public"`, `"authenticated"`, `"buyer"`, `"seller"`, `"staff"`, `"admin"` | Quyền hạn truy cập API (mặc định: `"public"`). |
| **`statusCode`** | Có | `200`, `201`, `400`, `404`, `422`, `500` | HTTP Status code trả về cho client. |
| **`responseType`** | Không | `"single"` (mặc định), `"paged"`, `"raw"`, `"error"` | Kiểu đóng gói phản hồi qua `ApiResponseFactory`. |
| **`message`** | Không | Chuỗi thông báo thành công bằng tiếng Anh (Sentence case) | Được gán vào trường `message` của `ApiResponse`. |
| **`request`** | Không | Object JSON | Payload mẫu hiển thị sẵn trên Swagger UI khi test. |
| **`response`** | Có | Object hoặc Array JSON | Dữ liệu trả về (sẽ được đưa vào trường `data` của `ApiResponse`). |

---

## 4. Cách Thêm Một Mock API Mới

1. Tạo file mới trong thư mục `Mocks/` với đuôi `.mock.json` (ví dụ: `my-feature.mock.json`).
2. Copy nội dung từ một trong 5 file mẫu (`auth-register.mock.json` hoặc `users-me.mock.json`).
3. Cập nhật `path`, `method`, `request`, `response` phù hợp với nghiệp vụ.
4. Mở Swagger UI (`http://localhost:8000/swagger`) $\rightarrow$ API mock mới sẽ tự động hiển thị và sẵn sàng test ngay mà không cần khởi động lại Gateway (Hot-Reload).
