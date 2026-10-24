# MẪU DỮ LIỆU VÀO/RA CHUẨN CỦA NEXUS API GATEWAY (input.md)
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — envelope `ApiResponse<T>`, `PagedData<T>` và `ApiErrorResponse` **đã có thật** trong code (`Nexus.Gateway/Responses/`) và khớp mẫu dưới đây; nhưng mới chỉ 4/15 controller có action sinh ra envelope này (16 action).
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này là **bộ mẫu JSON hợp đồng** (contract samples) dùng để viết mock, test và tài liệu Frontend. Ba mẫu tương ứng ba lớp thật trong code:
| Mẫu | Class thật | Bằng chứng |
|:---|:---|:---|
| 1. Success (một đối tượng) | `ApiResponse<T>` | `Responses/ApiResponse.cs` |
| 2. Phân trang | `ApiResponse<PagedData<T>>` + `PaginationMetadata` | `Responses/PagedData.cs`, `Responses/PaginationMetadata.cs` |
| 3. Thất bại | `ApiErrorResponse` (RFC 9110 Problem Details) | `Responses/ApiErrorResponse.cs`, `Middleware/GatewayExceptionHandler.cs` |
Quy ước bắt buộc khi dùng các mẫu này:
- `message`, `details`, `errors`, `errorCode` là **tuỳ chọn** — bị bỏ hẳn khỏi JSON khi `null` (`[JsonIgnore(Condition = WhenWritingNull)]`).
- `timestamp` luôn định dạng `yyyy-MM-ddTHH:mm:ssZ` (UTC).
- Enum được serialize thành **chuỗi PascalCase** (ví dụ `"Active"`, `"PendingPayment"`), không phải số — `Common/Converters/PascalCaseJsonStringEnumConverter.cs:13`, đăng ký tại `Extensions/CoreServicesExtension.cs:34,40`.
- Trường thất bại luôn có `traceId` để tra log/OTel; `type` mặc định là link RFC 9110, không phải domain riêng.
---
## 1. Success — API trả về một đối tượng
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User profile retrieved successfully.",
  "data": {
    "id": "11111111-1111-1111-1111-000000000004",
    "fullName": "Nguyen Van A",
    "email": "vana@example.com",
    "status": "Active"
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-24T08:25:00Z"
}
```
> Đã sửa so với bản cũ: `id` dùng **UUID** (không phải `usr_9981`) vì ID thật trong `Seeds/users/*.json` và `GatewayOptions.DefaultMockUserId` là UUID (`11111111-1111-1111-1111-000000000004`); `traceId` là trace id do ASP.NET cấp cho request, không phải chuỗi W3C `traceparent` đầy đủ.
---
## 2. Success — API có phân trang
```json
{
  "success": true,
  "statusCode": 200,
  "message": "Orders retrieved successfully.",
  "data": {
    "items": [
      { "id": "33333333-3333-3333-3333-000000000001", "orderCode": "ORD-001", "amount": 250000 },
      { "id": "33333333-3333-3333-3333-000000000002", "orderCode": "ORD-002", "amount": 500000 }
    ],
    "pagination": {
      "pageIndex": 1,
      "pageSize": 20,
      "totalCount": 245,
      "totalPages": 13,
      "hasPreviousPage": false,
      "hasNextPage": true
    }
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-24T08:25:00Z"
}
```
> Sáu trường trong `pagination` là **đúng 100%** tên property thật (`PaginationMetadata.cs`): `pageIndex`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage`. Giá trị biên được chuẩn hoá trong `PaginationMetadata.Create`: `pageIndex <= 0` ⇒ `1`, `pageSize <= 0` ⇒ `10`, `totalCount <= 0` ⇒ `totalPages = 0`; `pageSize` bị giới hạn 1..100 bởi validate phân trang.
> ⚠️ Route phân trang thật hiện chỉ có ở mức dispatcher (`IGatewayDispatcher.DispatchPagedAsync`) — chưa action nào của 4 controller đang hoạt động dùng tới.
---
## 3. Thất bại — lỗi validate / Problem Details
```json
{
  "type": "https://tools.ietf.org/html/rfc9110",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Username, valid email, and password with at least 8 characters are required.",
  "errors": {
    "Email": ["Email address is invalid."],
    "Password": ["Password must be at least 8 characters long."]
  },
  "traceId": "0HNPGT7G285Q2",
  "timestamp": "2026-10-24T08:25:00Z"
}
```
> Đã sửa so với bản cũ:
> - `type` đổi từ `https://errors.yourdomain.com/validation-error` (domain không tồn tại) sang giá trị mặc định thật của `ApiErrorResponse.Type` = `https://tools.ietf.org/html/rfc9110`.
> - Thông điệp chuyển sang **tiếng Anh, sentence case** (bản cũ trộn thông điệp tiếng Anh với dữ liệu tiếng Việt ở mẫu success).
> - `VAL_INVALID_INPUT` là mã mặc định thật của `Common/GatewayInputException.cs:9` và `Middleware/GatewayExceptionHandler.cs:61`.
### Bảng mã lỗi Gateway thật đang map (`Middleware/GatewayExceptionHandler.cs:61-80`)
| Tình huống | HTTP | `errorCode` | Bằng chứng |
|:---|:---:|:---|:---|
| gRPC `InvalidArgument` | 400 | `VAL_INVALID_INPUT` | `:61` |
| gRPC `Unauthenticated` | 401 | `AUTH_UNAUTHENTICATED` | `:62` |
| gRPC `PermissionDenied` | 403 | `AUTH_FORBIDDEN` | `:63` |
| gRPC `NotFound` | 404 | `NOT_FOUND` | `:64` |
| gRPC `AlreadyExists` / `Aborted` | 409 | `CONFLICT` | `:65` |
| gRPC `FailedPrecondition` | 422 | `FAILED_PRECONDITION` | `:66` |
| gRPC `ResourceExhausted` | 429 | `RATE_LIMIT_EXCEEDED` | `:67` |
| gRPC `Unimplemented` | 501 | `NOT_IMPLEMENTED` | `:68` |
| gRPC `Unavailable` | 503 | `SERVICE_UNAVAILABLE` | `:69` |
| gRPC `DeadlineExceeded` | 504 | `GATEWAY_TIMEOUT` | `:70` |
| gRPC status khác | 502 | `BAD_GATEWAY` | `:71` |
| `OperationCanceledException` (không do client hủy) | 504 | `GATEWAY_TIMEOUT` | `:74-75` |
| `BadHttpRequestException` | theo `bad.StatusCode` | `BAD_REQUEST` | `:77-78` |
| Ngoại lệ khác | 500 | `INTERNAL_ERROR` | `:79` |
| Circuit breaker: service `Unhealthy`/`Recovering` | 503 | `DOWNSTREAM_UNHEALTHY` | `Dispatching/GatewayDispatcher.cs:103-113` |
> ⚠️ Lỗi 401/403 do **auth middleware** (thiếu/sai token, thiếu quyền) trả về trước khi tới handler này, nội dung do ASP.NET Core JWT/RBAC sinh ra — **không** mang đúng `errorCode` ở bảng trên trừ khi phát sinh từ `RpcException`.