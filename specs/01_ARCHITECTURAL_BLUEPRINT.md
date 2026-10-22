# 01 — KIẾN TRÚC 3 KHỐI ĐỘC LẬP (3-TIER DECOUPLING BLUEPRINT)

Gateway được tái cấu trúc thành 3 khối phân tách triệt để, đảm bảo trách nhiệm đơn nhất (Single Responsibility Principle) và dễ dàng bảo trì mở rộng.

---

## 1. SƠ ĐỒ ĐIỀU PHỐI 3 TẦNG

```
[ HTTP Client Request ]
         │
         ▼
1. TẦNG ĐẦU VÀO: 11 Standard Controllers (Controllers/)
   - Nhận DTOs (trong Dtos/{Service}/*Requests.cs).
   - Kiểm tra input qua ValidationHelpers (Bad, PageOk, GuidOk).
   - Gọi 1 dòng: _dispatcher.DispatchAsync(...) hoặc _dispatcher.DispatchPagedAsync(...).
   - Bao gồm HealthProberController quản lý daemon giám sát sức khỏe & test thủ công.
         │
         ▼
2. TẦNG PHẦN GIỮA: The Dispatching Core (Dispatching/)
   - ModeEvaluator: Phân giải service và effective mode (Real / Mock).
   - MockDataMapper: Nạp file JSON từ Seeds/{service}/{entity}.json, thẩm định qua MessageParser<T>.ParseJson().
   - GrpcServiceCaller: Đính kèm Authorization, Idempotency-Key, x-trace-id và gọi typed gRPC Client qua HTTP/2.
   - GatewayDispatcher: Nhận lệnh -> phối hợp 3 bộ trên -> chuyển dữ liệu gốc sang Đầu Ra.
         │
         ▼
3. TẦNG ĐẦU RA: Universal Output & Exception Handling (Responses/ & Common/)
   - ApiResponseFactory: Không quan tâm data từ Mock hay Real, tự động bọc ApiResponse<T> hoặc ApiResponse<PagedData<T>>, gắn traceId và ISO timestamp.
   - GatewayExceptionHandler: Bắt mọi lỗi RpcException, Timeout, Validation map sang ApiErrorResponse RFC 9110 / RFC 6585.
         │
         ▼
[ HTTP JSON Response ]
```

---

## 2. BẢN ĐỒ CÁC LỚP TRONG DISPATCHING CORE

1. **`IModeEvaluator` / `ModeEvaluator`**:
   - Truy vấn cấu hình từ `IOptionsMonitor<GatewayOptions>`.
   - Kết hợp với `IGatewayRuntimeManager` để hỗ trợ Hot-Reload từ `gateway.runtime.json` và Header Override (`X-Service-Mode-{service}: Real`).

2. **`IMockDataMapper` / `MockDataMapper`**:
   - Quét tìm file seed JSON: Ưu tiên `Seeds/{service}/{operation}.json`, dự phòng `Mocks/{service}/{operation}.json`.
   - Áp dụng `MockDelayMs` và `MockStatus` nếu được cấu hình.
   - Parse JSON qua `MessageParser<T>.ParseJson()` $\rightarrow$ Nếu file seed JSON sai kiểu dữ liệu hoặc thiếu trường so với Protobuf, lỗi sẽ bị bắt ngay lập tức.

3. **`IGrpcServiceCaller` / `GrpcServiceCaller`**:
   - Tạo `CallOptions` với `deadline` dựa trên `TimeoutSeconds`.
   - Forward `Authorization: Bearer <token>`, `Idempotency-Key`, và `x-trace-id`.
   - Thực thi cuộc gọi gRPC bất đồng bộ.

4. **`IGatewayDispatcher` / `GatewayDispatcher`**:
   - Tích hợp **Circuit Breaker Fast-Fail** cùng `IHealthProberManager`: Nếu service con bị Unhealthy hoặc đang Recovering, lập tức ngắt mạch trong 0ms trả về HTTP 503 `DOWNSTREAM_UNHEALTHY`.
   - Cung cấp hai phương thức điều phối duy nhất:
     - `Task<IResult> DispatchAsync<T>(...)`: Cho các endpoint trả về đối tượng đơn lẻ.
     - `Task<IResult> DispatchPagedAsync<T>(...)`: Cho các endpoint trả về danh sách phân trang có `pageIndex`, `pageSize`, `totalCount`.

5. **`BackendDispatcher`**:
   - Cầu nối tương thích ngược (compatibility wrapper) ủy quyền trực tiếp sang `IGatewayDispatcher`.
