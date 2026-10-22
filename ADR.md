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
  $$\mathbf{/api/v1/\{service\}/...}$$
- **Nội bộ (Gateway $\rightarrow$ Microservices)**: Chuẩn **gRPC trên nền Protobuf v3** (`Contracts/shop.proto`) an toàn kiểu dữ liệu, nhị phân nén, độ trễ cực thấp.

---

## ADR 03: Kiến Trúc 3 Khối Độc Lập Cho API Gateway (3-Tier Decoupling)
- **Khối Đầu Vào (Input)**: 10 Controllers tiêu chuẩn `[ApiController] ControllerBase` trong `Controllers/`, nhóm theo 8 microservices. Nhận Request DTOs, tự động validate cơ bản thông qua ModelBinding/DataAnnotations và các helper `EnsureValid()`, `EnsureGuid()`, `EnsurePositive()`. Tuyệt đối không chứa logic `if Mock else Real` hay các khối `try-catch` / `.Error(...)` thủ công làm phình to action.
- **Tuyến security trước dispatcher**: sau rewrite `/gateway/api/v1/...` sang `/api/v1/...` và routing, adapter/authentication/rate-limit/authorization nhận diện service và effective mode để quyết định quyền; declarative mock hoặc controller chỉ xử lý sau đó. `GatewayRequestResolution` ghi một snapshot per-request ngay sau routing và trước authentication: endpoint metadata có ưu tiên với auth/rate-limit, mock matcher được giữ lại cho declarative route; effective mode cho từng service được cache theo request. Auth mode là trục độc lập, không được suy từ snapshot service mode. Khi route controller và mock definition trùng nhưng khác service, security giữ service metadata của controller, mock engine dùng service của definition; cần giữ regression tests cho trường hợp chồng lấn.
- **Khối Phần Giữa (Dispatching Core)**:
  - `ModeEvaluator`: Xác định trạng thái Mock hay Real theo service (hỗ trợ file runtime hot-reload và header override).
  - `MockDataMapper`: Đọc file Seed JSON trong `Seeds/{service}/{entity}.json`, kiểm định cú pháp qua Protobuf MessageParser.
  - `GrpcServiceCaller`: Tự động forward Metadata (JWT Bearer, Idempotency-Key, TraceId) và gọi gRPC typed client.
  - `GatewayDispatcher`: Chọn fixture Mock hoặc gRPC Real và dùng response factory trả `IResult` với HTTP status đúng (200 mặc định, 201 khi action chỉ định).
- **Khối Đầu Ra (Output)**:
  - `ApiResponseFactory`: Universal factory đóng gói `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`. Cung cấp các static helpers `Success<T>` và `Error<T>` trả về envelope thống nhất.
  - `GatewayExceptionHandler`: Tự động bắt và map tập trung `GatewayInputException`, `RpcException`, timeout và lỗi hệ thống thành response lỗi chuẩn RFC 9110/ApiErrorResponse kèm `traceId` và HTTP status tương ứng (400, 401, 403, 404, 409, 422, 503, 504). Controller hoàn toàn không cần bọc lỗi thủ công.

---

## ADR 04: Bản Chất Dữ Liệu Mock/Seed Tại Gateway
- **Quyết định**: Gateway **không ôm database in-memory hay tính toán stateful logic** của backend.
- **Lý do**: Giữ Gateway mỏng (thin edge proxy), độ tin cậy cao, không bị phình to bộ nhớ.
- **Triển khai**: Thư mục `Seeds/{service}/{entity}.json` đóng vai trò là contract stub fixtures, được Protobuf Parser kiểm tra kiểu/schema; cần test contract và backend để xác minh ý nghĩa nghiệp vụ.

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

---

## ADR 09: Chiến Lược Phòng Thủ Xác Thực Dữ Liệu 3 Tầng (3-Tier Validation Defense-in-Depth)
- **Bối cảnh & Vấn đề**: Ranh giới xác thực dữ liệu cần được phân định rõ ràng giữa Gateway, Backend Service API/Application và Domain/Database để tránh Gateway bị phình to bởi logic nghiệp vụ của các service khác.
- **Quyết định phân tầng xác thực**:
  1. **Tầng 1 (Edge Gateway — Kiểm tra mức HTTP / Hạ tầng)**:
     - Định dạng HTTP cơ bản, Content-Type (`application/json`), giới hạn body size (64KB), Rate Limiting.
     - Xác thực JWT token cơ bản, chuyển tiếp danh tính xuống gRPC metadata.
     - Các header hạ tầng bắt buộc: Header `Idempotency-Key` (UUID format), giới hạn phân trang query (`page >= 1`, `pageSize` 1..100).
     - *Lưu ý trong Mock Mode*: Gateway chỉ thực hiện các validation cơ bản này làm "Mock Gatekeeper" để phục vụ các bài test giả lập khi backend chưa chạy, không can thiệp sâu vào nghiệp vụ riêng của từng domain.
  2. **Tầng 2 (Service API & Application Layer — Validate DTOs & Use Case Input)**:
     - Áp dụng `FluentValidation` hoặc `Data Annotations` tại tầng API/Application của từng microservice: `quantity > 0`, `price >= 0`, Email đúng định dạng regex, các trường bắt buộc không để trống, độ dài chuỗi tối đa/tối thiểu.
     - Khi vi phạm: Ném lỗi gRPC `RpcException(StatusCode.InvalidArgument)` kèm từ điển lỗi chi tiết `errors: { Field: [Message] }`. GatewayExceptionHandler tại Gateway sẽ tự động bắt và map sang HTTP `400 Bad Request` chuẩn RFC 9110.
  3. **Tầng 3 (Domain & Database — Bảo Vệ Bất Biến Nghiệp Vụ & Toàn Vẹn Dữ Liệu)**:
     - **Domain Entities**: Đóng gói quy tắc nghiệp vụ bất biến (Business Invariants) ngay trong Entity methods (ví dụ: số lượng đơn hàng phải $> 0$, không cho phép chuyển trạng thái đơn hàng trái quy tắc, tồn kho không được âm khi trừ).
     - **Database Constraints**: Đặt các ràng buộc `CHECK (available >= 0)`, `NOT NULL`, `UNIQUE`, `FOREIGN KEY` ở tầng DB PostgreSQL.
     - **Đảm bảo độc lập**: Dù request đến từ Gateway, từ gRPC nội bộ giữa 2 service, hay từ RabbitMQ Consumer, tính toàn vẹn nghiệp vụ và dữ liệu luôn được bảo vệ 100%.
---

## ADR 10: Chuẩn Hóa Distributed Tracing (OpenTelemetry & Grafana Tempo) & Ranh Giới Hạ Tầng
- **Bối cảnh & Vấn đề**:
  Hệ thống gồm API Gateway và 8 Microservices giao tiếp qua REST JSON, gRPC HTTP/2, và RabbitMQ (MassTransit). Khi có lỗi phân tán hoặc suy giảm hiệu năng liên service, việc đọc log rời rạc từng container là không khả thi. Cần một giải pháp Distributed Tracing toàn diện theo chuẩn quốc tế nhưng **tuyệt đối không làm phức tạp hóa code nghiệp vụ của developer**.
- **Quyết định kiến trúc**:
  1. **Chuẩn ngữ cảnh W3C Trace Context**: Sử dụng tiêu chuẩn công nghiệp W3C Trace Context (`traceparent`). gRPC Client/Server và HTTP Instrumentation tự động đính kèm và lan truyền mã vết này xuyên suốt qua các tầng mạng.
  2. **OpenTelemetry .NET SDK với Auto-Instrumentation (Zero-Touch cho Dev)**:
     - Mọi service (Gateway và 8 Microservices con) đều tích hợp OpenTelemetry SDK.
     - Sử dụng cơ chế tự động bắt vết ngầm: HTTP In/Out, gRPC Calls, Entity Framework Core SQL queries, Redis cache, và MassTransit RabbitMQ messages.
     - **Nguyên lý Zero-Touch**: Developer viết nghiệp vụ (Controller, gRPC Service, Domain logic, Event Consumer) **không cần viết bất kỳ dòng code tracing nào**. Tracing được kích hoạt tự động chỉ với 1 dòng khai báo duy nhất tại `Program.cs`.
  3. **Backend Lưu Trữ & Trực Quan Hóa (Observability Stack)**:
     - **Grafana Tempo**: Đóng vai trò là OTLP Trace Backend tập trung (lắng nghe cổng gRPC `4317` và HTTP `4318`). Tempo cực nhẹ, không yêu cầu Elasticsearch/Cassandra nặng nề, phù hợp cả local dev lẫn production.
     - **Grafana Dashboard**: Mở giao diện tại cổng `3000`, tích hợp sẵn datasource Tempo để tra cứu Trace theo mã `traceId` hoặc trực quan hóa cây luồng thời gian thực (Flame Graph).
  4. **Phân định rõ ranh giới hạ tầng nền tảng (Infrastructure Separation)**:
     - **SSL/TLS Termination**: Không cấu hình bên trong C# Gateway. Đây là trách nhiệm của lớp Ingress Proxy biên mạng (Cloudflare, AWS ALB, Nginx). Gateway chỉ chạy HTTP nội bộ tại cổng `8000`.
     - **Identity Provider (IdP) & Context Propagation**: Gateway đảm nhiệm xác thực JWT Bearer token ở cổng vào. Khi gọi gRPC xuống các microservices, Gateway tự động giải mã và gắn thông tin định danh vào gRPC Metadata:
       - `x-user-id`: User ID định danh (`sub` claim).
       - `x-user-roles`: Danh sách vai trò (`role` claims), cách nhau bởi dấu phẩy.
       - `x-user-email`: Email người dùng.
       - `x-trace-id`: Trace ID nhất quán của request.
     - **Lợi ích**: Các microservices con **không cần giải mã lại JWT**, giảm thiểu tính toán CPU lặp lại và chỉ cần lấy thông tin user trực tiếp từ `ServerCallContext.RequestHeaders`.
  5. **Quy tắc bắt buộc khi tạo Microservice mới**:
     - Mọi microservice mới khi đưa vào hệ sinh thái Nexus **bắt buộc** phải gọi `builder.Services.AddServiceObservability(builder.Configuration, "Tên_Service");` ngay trong `Program.cs` theo hướng dẫn trong [SETUP_GUIDE_SERVICES.md](SETUP_GUIDE_SERVICES.md).

---

## ADR 11: Cơ Chế Giám Sát Sức Khỏe (Health Prober Daemon), Tự Phục Hồi (Self-Healing) & Chiến Lược Log Sự Cố
- **Bối cảnh & Vấn đề**:
  Gateway làm điểm vào duy nhất nhưng cần chủ động biết trạng thái sống/chết (Health Probe) của 8 microservices con. Khi một service bị treo RAM/thread pool hoặc crash, nếu tiếp tục đẩy request sẽ làm treo Kestrel threads và cạn kiệt kết nối mạng. Đồng thời, việc ghi log ồ ạt cho các lần kiểm tra bình thường sẽ gây nghẽn đĩa cứng và làm đầy dung lượng (Log Pollution).
- **Quyết định kiến trúc**:
  1. **Tác nhân thăm dò chạy nền (External Health Prober Daemon Engine)**:
     - Tích hợp `HealthProberManager` chạy như một Hosted Service trong Gateway.
     - Mặc định khởi động ở trạng thái **TẮT (`Disabled`)**; chỉ chạy khi được kích hoạt qua Control API hoặc cấu hình.
  2. **Bốn tham số chuẩn hóa**:
     - `Interval`: 60 giây (1 phút).
     - `Timeout`: 30 giây.
     - `Retries / FailureThreshold`: 5 lần lỗi liên tiếp trước khi chuyển `Unhealthy`.
     - `StartPeriod`: 60 giây ân hạn khi container mới bật lại (không tính lỗi vào Retries).
  3. **State Machine chặt chẽ**:
     - `Disabled` $\rightarrow$ `Starting` $\rightarrow$ `Healthy` $\leftrightarrow$ `Degraded` (1..4 lỗi) $\rightarrow$ `Unhealthy` (5/5 lỗi) $\rightarrow$ `Recovering`.
  4. **Chiến lược phân tầng lưu log**:
     - *Healthy định kỳ*: **Không ghi file log**. Chỉ giữ Ring Buffer 30 mẫu trong RAM để phục vụ API và đẩy metrics.
     - *Sự cố (Degraded & Unhealthy)*: Ghi vào `logs/health/incident-YYYYMMDD-XXXX.log` với cơ chế xoay file tối đa **1000 dòng/file**.
     - *Định hướng tương lai cho `LogService`*: Gom cụm tối thiểu 1000 dòng (Batching Sink) trước khi gửi bulk một lần qua mạng.
  5. **Circuit Breaker Fast-Fail tại `GatewayDispatcher`**:
     - Khi service ở `Unhealthy` hoặc `Recovering`, Gateway ngắt mạch tức thì trong 0ms, trả về HTTP 503 `DOWNSTREAM_UNHEALTHY` theo chuẩn RFC 9110 Problem Details.
  6. **Saga Rollback & Docker Container Auto-Recovery**:
     - Publish sự kiện `ServiceUnhealthyIntegrationEvent` lên RabbitMQ để hủy các giao dịch đang treo.
     - Giao tiếp với Docker socket `/var/run/docker.sock` để Stop/Remove/Recreate container `nexus-{service}-service` (giới hạn tối đa 3 lần chống CrashLoopBackOff).
  7. **Controller Quản Trị Mới (11th Controller)**:
     - Bổ sung `HealthProberController` (`/api/v1/gateway/prober/...`) hỗ trợ xem trạng thái, bật/tắt từng daemon, bulk toggle và **API kiểm tra sức khỏe thủ công tức thì (Manual Check on demand)**.
