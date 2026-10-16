# Architecture Decision Records (ADR.md) — Nexus API Gateway & Microservices

Tài liệu này ghi nhận các quyết định kiến trúc cốt lõi định hình hệ sinh thái Microservices và API Gateway của Nexus.

---

## ADR 01: Nền Tảng Công Nghệ & Khung Ứng Dụng
- **Quyết định**: Sử dụng **.NET 8 (C# 12)** cho toàn bộ hệ thống.
- **Lý do**: Hiệu năng cao, hỗ trợ native AOT/HTTP3/gRPC tốt nhất, chu kỳ hỗ trợ dài hạn (LTS).
- **Phạm vi**: Toàn bộ 8 Microservices và ApiGateway.

---

## ADR 02: Mô Hình Giao Tiếp Ngoại Vi & Nội Bộ (Restful vs gRPC)
- **Ngoại vi (Client $\rightarrow$ Gateway)**: Chuẩn **RESTful JSON** qua HTTP/1.1 và HTTP/2 với tiền tố đường dẫn thống nhất:
  $$\mathbf{/gateway/api/v1/\{service\}/...}$$
- **Nội bộ (Gateway $\rightarrow$ Microservices)**: Chuẩn **gRPC trên nền Protobuf v3** (`Contracts/shop.proto`) an toàn kiểu dữ liệu, nhị phân nén, độ trễ cực thấp.

---

## ADR 03: Kiến Trúc 3 Khối Độc Lập Cho API Gateway (3-Tier Decoupling)
- **Khối Đầu Vào (Input)**: 10 Controllers tiêu chuẩn `[ApiController] ControllerBase` trong `Controllers/`, nhóm theo 8 microservices. Nhận Request DTOs, kiểm tra input cơ bản, tuyệt đối không chứa logic `if Mock else Real`.
- **Khối Phần Giữa (Dispatching Core)**:
  - `ModeEvaluator`: Xác định trạng thái Mock hay Real theo service (hỗ trợ file runtime hot-reload và header override).
  - `MockDataMapper`: Đọc file Seed JSON trong `Seeds/{service}/{entity}.json`, kiểm định cú pháp qua Protobuf MessageParser.
  - `GrpcServiceCaller`: Tự động forward Metadata (JWT Bearer, Idempotency-Key, TraceId) và gọi gRPC typed client.
  - `GatewayDispatcher`: Điều phối chung, kết hợp 3 khối và trả về kết quả chuẩn.
- **Khối Đầu Ra (Output)**:
  - `ApiResponseFactory`: Universal factory đóng gói `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`.
  - `GatewayExceptionHandler`: Hứng mọi ngoại lệ tập trung, chuyển đổi lỗi gRPC sang RFC 9110 / RFC 6585.

---

## ADR 04: Bản Chất Dữ Liệu Mock/Seed Tại Gateway
- **Quyết định**: Gateway **không ôm database in-memory hay tính toán stateful logic** của backend.
- **Lý do**: Giữ Gateway mỏng (thin edge proxy), độ tin cậy cao, không bị phình to bộ nhớ.
- **Triển khai**: Thư mục `Seeds/{service}/{entity}.json` đóng vai trò là contract stub fixtures, được Protobuf Parser kiểm tra tính toàn vẹn 100%.

---

## ADR 05: Phân Tách DTOs (Requests & Responses)
- **Quyết định**: Thay thế toàn bộ `Models` thành `Dtos/`, chia theo từng service và tách biệt thành 2 file riêng:
  - `Dtos/{Service}/{Service}Requests.cs` (những gì Client gửi lên).
  - `Dtos/{Service}/{Service}Responses.cs` (những gì Client nhận về).

---

## ADR 06: Bảo Mật, Xác Thực (JWT) & Phân Quyền (RBAC)
- **Mã hóa mật khẩu**: Sử dụng Argon2 (hoặc BCrypt chuẩn an toàn cao).
- **JWT Bearer Token**: Ký HMAC-SHA256 hoặc Asymmetric RSA.
- **Phân quyền vai trò (RBAC)**: Hỗ trợ ma trận quyền `buyer`, `seller`, `staff`, `admin`, `gateway-admin`.
- **Cơ chế 2 trạng thái Gateway (Gateway State)**:
  - `Standard`: Yêu cầu token và kiểm tra quyền RBAC nghiêm ngặt (chặn 401/403).
  - `Bypass`: Dành riêng cho môi trường Development để Frontend test giao diện và mock APIs độc lập mà không cần login.

---

## ADR 07: Giới Hạn Tần Suất Gọi Phân Tán (Distributed Rate Limiting)
- **Quyết định**: Áp dụng Sliding Window Rate Limiting dựa trên Redis (`StackExchange.Redis`).
- **Khả năng chịu lỗi (Resilience)**: Ở môi trường Development, áp dụng chính sách **Fail-Open** (nếu Redis chưa bật hoặc mất kết nối, Gateway vẫn cho phép request đi qua và ghi log cảnh báo).

---

## ADR 08: Kiến Trúc Dữ Liệu Microservices Backend (No Repository Pattern)
- **ORM**: Entity Framework Core 8 Code-First (PostgreSQL / SQL Server).
- **Repository Pattern**: **Không sử dụng Repository / Unit of Work bọc ngoài EF Core**, sử dụng trực tiếp `DbContext` và `DbSet` để tận dụng tối đa Change Tracker, Linq, IQueryable và async API của EF Core.
- **Event Bus & Messaging**: RabbitMQ kết hợp thư viện **MassTransit 8.4.1**.
- **Tính toàn vẹn dữ liệu**: Outbox Pattern cho các thao tác bắn event và Inbox Pattern cho consumer tránh duplicate message.
- **Giao dịch phân tán**: Saga State Machine Pattern điều phối các luồng nghiệp vụ phức tạp liên service (Checkout $\rightarrow$ Reserve Inventory $\rightarrow$ Deduct Wallet $\rightarrow$ Confirm Order).
