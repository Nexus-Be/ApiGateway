# Architecture Decision Records (ADR.md) — Nexus API Gateway & Microservices
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 4/15 controller có action (16 action), 11 stub rỗng; 1/8 gRPC client; `shop.proto` chỉ có `UsersApi` 5 rpc; 62 attribute test trên 19 file. Đây là **tài liệu quyết định kiến trúc**: phần "Quyết định" giữ nguyên hiệu lực, phần "Hiện trạng" phản ánh code thật.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
Tài liệu này ghi nhận các quyết định kiến trúc cốt lõi định hình hệ sinh thái Microservices và API Gateway của Nexus. Mỗi ADR gồm: **Quyết định** (đích đến đã chốt) và **Hiện trạng** (đo từ code, có nhãn ✅/⚠️/❌).
| ADR | Chủ đề | Hiện trạng trong repo này |
|:---|:---|:---|
| 01 | .NET 8 / C# 12 | ✅ CÓ — `net8.0` |
| 02 | REST ngoại vi + gRPC nội bộ | ⚠️ MỘT PHẦN — chỉ 1 service có client gRPC |
| 03 | Kiến trúc 3 khối Gateway | ⚠️ MỘT PHẦN — 4/15 controller có action |
| 04 | Mock/Seed không stateful | ✅ CÓ — `MockDataMapper` đọc file, 0 DB |
| 05 | Tách DTOs 2 file/service | ⚠️ MỘT PHẦN — 4/8 domain |
| 06 | JWT + RBAC | ⚠️ MỘT PHẦN — validate được, không service nào phát token |
| 07 | Rate limit Redis + fail-open dev | ✅ CÓ |
| 08 | EF Core trực tiếp + MassTransit/Saga | ❌ CHƯA CÓ trong gateway (0 package) |
| 09 | Validate 3 tầng | ⚠️ MỘT PHẦN — tầng 1 đầy đủ, tầng 2/3 thuộc backend |
| 10 | OTel + Tempo + header danh tính | ⚠️ MỘT PHẦN — chỉ Gateway có OTel package |
| 11 | Health Prober + tự phục hồi | ⚠️ MỘT PHẦN — prober ✅, Saga ❌, docker CLI ❌ |
---
## ADR 01: Nền Tảng Công Nghệ & Khung Ứng Dụng
- **Quyết định**: Sử dụng **.NET 8 (C# 12)** cho toàn bộ hệ thống.
- **Lý do**: Hiệu năng cao, hỗ trợ native AOT/HTTP3/gRPC tốt nhất, chu kỳ hỗ trợ dài hạn (LTS).
- **Phạm vi**: Toàn bộ 8 Microservices và ApiGateway.
- **Hiện trạng**: ✅ CÓ — `Nexus.Gateway/Nexus.Gateway.csproj` và `Nexus.Gateway.Tests/Nexus.Gateway.Tests.csproj` cùng target `net8.0`.
---
## ADR 02: Mô Hình Giao Tiếp Ngoại Vi & Nội Bộ (Restful vs gRPC)
- **Ngoại vi (Client $\rightarrow$ Gateway)**: Chuẩn **RESTful JSON** qua HTTP/1.1 và HTTP/2 với tiền tố đường dẫn thống nhất:
  $$\mathbf{/api/v1/\{service\}/...}$$
- **Nội bộ (Gateway $\rightarrow$ Microservices)**: Chuẩn **gRPC trên nền Protobuf v3** (`Contracts/shop.proto`) an toàn kiểu dữ liệu, nhị phân nén, độ trễ cực thấp.
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - REST ngoại vi: ✅ CÓ — 16 action trên 4 controller (`AuthController.cs:29,61,84,106`; `UsersController.cs:27`; `GatewayControlController.cs:34,42,58,74,101,120`; `HealthProberController.cs:33,51,79,110,124`).
  - gRPC nội bộ: ⚠️ MỘT PHẦN — chỉ **1/8** client được đăng ký (`Extensions/GrpcClientsExtension.cs` → `AddNamedGrpcClient<UsersApi.UsersApiClient>("users")`); 7 service còn lại chưa có typed client nên chuyển `Mode: "Real"` sẽ không gọi được backend.
  - Hợp đồng: ⚠️ MỘT PHẦN — `Nexus.Gateway/Contracts/shop.proto` chỉ khai báo `service UsersApi` (5 rpc), **không có** `CatalogApi`/`WalletApi`/… Dạng đường dẫn thật của controller cũng không đồng nhất `/api/v1/{service}/...` (ví dụ `api/v1/users/me` nằm dưới `[Route("api/v1")]`, `api/v1/auth`, `api/v1/commerce/cart`, `api/v1/catalog/products`).
---
## ADR 03: Kiến Trúc 3 Khối Độc Lập Cho API Gateway (3-Tier Decoupling)
- **Khối Đầu Vào (Input)**: **15 controller** `[ApiController] ControllerBase` trong `Controllers/` (555 dòng tổng), nhóm theo 8 microservices. Nhận Request DTOs, tự động validate cơ bản thông qua ModelBinding/DataAnnotations và các helper `EnsureValid()`, `EnsureGuid()`, `EnsurePositive()`. Tuyệt đối không chứa logic `if Mock else Real` hay các khối `try-catch` / `.Error(...)` thủ công làm phình to action.
- **Tuyến security trước dispatcher**: sau rewrite `/gateway/api/v1/...` sang `/api/v1/...` và routing, adapter/authentication/rate-limit/authorization nhận diện service và effective mode để quyết định quyền; declarative mock hoặc controller chỉ xử lý sau đó. `GatewayRequestResolution` ghi một snapshot per-request ngay sau routing và trước authentication: endpoint metadata có ưu tiên với auth/rate-limit, mock matcher được giữ lại cho declarative route; effective mode cho từng service được cache theo request. Auth mode là trục độc lập, không được suy từ snapshot service mode. Khi route controller và mock definition trùng nhưng khác service, security giữ service metadata của controller, mock engine dùng service của definition; cần giữ regression tests cho trường hợp chồng lấn.
- **Khối Phần Giữa (Dispatching Core)**:
  - `ModeEvaluator`: Xác định trạng thái Mock hay Real theo service (hỗ trợ file runtime hot-reload và header override).
  - `MockDataMapper`: Đọc file Seed JSON trong `Seeds/{service}/{entity}.json`, kiểm định cú pháp qua Protobuf MessageParser.
  - `GrpcServiceCaller`: Tự động forward Metadata (JWT Bearer, Idempotency-Key, TraceId) và gọi gRPC typed client.
  - `GatewayDispatcher`: Chọn fixture Mock hoặc gRPC Real và dùng response factory trả `IResult` với HTTP status đúng (200 mặc định, 201 khi action chỉ định).
- **Khối Đầu Ra (Output)**:
  - `ApiResponseFactory`: Universal factory đóng gói `ApiResponse<T>` hoặc `ApiResponse<PagedData<T>>`. Cung cấp các static helpers `Success<T>` và `Error<T>` trả về envelope thống nhất.
  - `GatewayExceptionHandler`: Tự động bắt và map tập trung `GatewayInputException`, `RpcException`, timeout và lỗi hệ thống thành response lỗi chuẩn RFC 9110/ApiErrorResponse kèm `traceId` và HTTP status tương ứng (400, 401, 403, 404, 409, 422, 503, 504). Controller hoàn toàn không cần bọc lỗi thủ công.
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - Khối Input: ⚠️ MỘT PHẦN — 15 file controller nhưng **chỉ 4 file có action (16 action)**: `AuthController` 129 dòng/4 action, `GatewayControlController` 127/6, `HealthProberController` 147/5, `UsersController` 42/1. **11 file còn lại là stub đúng 10 dòng, 0 action**, chỉ có 1 dòng `// TODO (Team): Triển khai các Action Mock/Real ...` (`AdminUsersController`, `AuctionController`, `CartController`, `CategoriesController`, `FulfillmentController`, `InventoryController`, `LogController`, `NotificationController`, `OrdersController`, `ProductsController`, `WalletController`).
  - Khối Dispatching: ✅ CÓ — `Dispatching/{ModeEvaluator,MockDataMapper,GrpcServiceCaller,GatewayDispatcher}.cs` + `BackendDispatcher.cs`, DI tại `Extensions/CoreServicesExtension.cs:73`.
  - Khối Output: ✅ CÓ — `Responses/ApiResponseFactory.cs`, `Middleware/GatewayExceptionHandler.cs`.
  - Hệ quả cần biết: 11 route (`api/v1/admin/users`, `api/v1/auction`, `api/v1/commerce/cart`, `api/v1/catalog/categories`, `api/v1/fulfillment`, `api/v1/catalog/inventory`, `api/v1/log`, `api/v1/notification`, `api/v1/commerce/orders`, `api/v1/catalog/products`, `api/v1/wallet`) hiện trả **404** trừ khi có `Mocks/*.mock.json` khớp path.
---
## ADR 04: Bản Chất Dữ Liệu Mock/Seed Tại Gateway
- **Quyết định**: Gateway **không ôm database in-memory hay tính toán stateful logic** của backend.
- **Lý do**: Giữ Gateway mỏng (thin edge proxy), độ tin cậy cao, không bị phình to bộ nhớ.
- **Triển khai**: Thư mục `Seeds/{service}/{entity}.json` đóng vai trò là contract stub fixtures, được Protobuf Parser kiểm tra kiểu/schema; cần test contract và backend để xác minh ý nghĩa nghiệp vụ.
- **Hiện trạng**: ✅ CÓ — 0 `DbSet`, 0 `DbContext`, 0 package EF Core trong `Nexus.Gateway.csproj`; `MockDataMapper.cs:64-76,126-138` đọc file seed bằng `File.ReadAllTextAsync` mỗi request. Dữ liệu thật hiện có: 9 file seed (`Seeds/auth/` 4, `Seeds/users/` 5) + 6 file declarative (`Mocks/*.mock.json`); 3 thư mục `Seeds/{cart,orders,products}/` chỉ có `README.md`.
---
## ADR 05: Phân Tách DTOs (Requests & Responses)
- **Quyết định**: Thay thế toàn bộ `Models` thành `Dtos/`, chia theo từng service và tách biệt thành 2 file riêng:
  - `Dtos/{Service}/{Service}Requests.cs` (những gì Client gửi lên).
  - `Dtos/{Service}/{Service}Responses.cs` (những gì Client nhận về).
- **Hiện trạng**: ⚠️ MỘT PHẦN — `Nexus.Gateway/Dtos/` chỉ có **4 thư mục / 4 domain**: `Auth`, `GatewayControl`, `HealthProber`, `Users`; 4 domain còn lại (Catalog, Commerce, Auction, Wallet, Log, Fulfillment, Notification) **chưa có DTO** vì controller tương ứng còn stub. Không có thư mục `Models/` nào trong repo (đã thay thế xong).
---
## ADR 06: Bảo Mật, Xác Thực (JWT) & Phân Quyền (RBAC)
- **Mã hóa mật khẩu**: Sử dụng Argon2 (hoặc BCrypt chuẩn an toàn cao).
- **JWT Bearer Token**: Ký HMAC-SHA256 hoặc Asymmetric RSA.
- **Phân quyền vai trò (RBAC)**: Hỗ trợ ma trận quyền `buyer`, `seller`, `staff`, `admin`, `gateway-admin`.
- **Cơ chế 2 trạng thái Gateway (Gateway State)**:
  - `Standard`: Yêu cầu token và kiểm tra quyền RBAC nghiêm ngặt (chặn 401/403).
  - `Bypass`: Dành riêng cho môi trường Development để Frontend test giao diện và mock APIs độc lập mà không cần login.
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - Trục cấu hình thật là **`Gateway:AuthMode`** (`GatewayOptions.AuthMode`, `GatewayOptions.cs:38`) với giá trị `Enforce` (mặc định, `:101`) / `Standard` / `Bypass`; `Enforce`+`Standard` luôn hợp lệ, còn `Bypass`/`Mock`/`MockBypass` **chỉ hợp lệ khi Development** (`:110-115`). `GatewayMode` vẫn được đọc như alias legacy (`:42`) — cấu hình mới phải dùng `AuthMode`.
  - ⚠️ `appsettings.json` đang lưu **JWT signing key thật** ở `Jwt.Signature`; file này **đang bị `.gitignore:81` (`appsettings*.json`) bỏ qua** nên **không bị commit** (`git grep` = 0 hit). Rủi ro thật không nằm ở git mà ở chỗ khoá dev dùng chung và nằm plaintext trên máy. Hai file `*.example.json` dùng placeholder `<REPLACE_WITH_JWT_SIGNING_KEY_MIN_32_CHARS>` — hãy cấp khoá qua `Jwt__Signature` / secret store.
  - ✅ RBAC: `Security/SecurityConfiguration.cs:156-161` đăng ký policy `authenticated`, `customer`, `seller`, `staff`, `admin`, `gateway-admin` (`GatewayPolicies`); `GatewayControlController`/`HealthProberController` gắn `[Authorize(GatewayPolicies.GatewayAdmin)]`.
  - ❌ Chưa có service nào phát JWT: `UsersApi.Login` chưa implement ở `User` service (GW-03), nên luồng login end-to-end chưa chạy được.
---
## ADR 07: Giới Hạn Tần Suất Gọi Phân Tán (Distributed Rate Limiting)
- **Quyết định**: Áp dụng Sliding Window Rate Limiting dựa trên Redis (`StackExchange.Redis`).
- **Khả năng chịu lỗi (Resilience)**: Ở môi trường Development, áp dụng chính sách **Fail-Open** (nếu Redis chưa bật hoặc mất kết nối, Gateway vẫn cho phép request đi qua và ghi log cảnh báo).
- **Hiện trạng**: ✅ CÓ — `Middleware/RedisRateLimitMiddleware.cs` + `StackExchange.Redis 2.8.24`; cờ fail-open thật là `Gateway:RedisFailOpenInDevelopment` (bật `true` trong `gateway.runtime.json` và `appsettings.Development.json`, `false` trong `appsettings.json`); policy `"api"` khai báo qua `[EnableRateLimiting("api")]` trên 3 controller.
---
## ADR 08: Kiến Trúc Dữ Liệu Microservices Backend (No Repository Pattern)
- **ORM**: Entity Framework Core 8 Code-First (PostgreSQL / SQL Server).
- **Repository Pattern**: **Không sử dụng Repository / Unit of Work bọc ngoài EF Core**, sử dụng trực tiếp `DbContext` và `DbSet` để tận dụng tối đa Change Tracker, Linq, IQueryable và async API của EF Core.
- **Event Bus & Messaging**: RabbitMQ kết hợp thư viện **MassTransit 8.3.0** (phiên bản thống nhất trong toàn bộ `.csproj` backend).
- **Tính toàn vẹn dữ liệu**: Outbox Pattern cho các thao tác bắn event và Inbox Pattern cho consumer tránh duplicate message.
- **Giao dịch phân tán**: Saga State Machine Pattern điều phối các luồng nghiệp vụ phức tạp liên service (Checkout $\rightarrow$ Reserve Inventory $\rightarrow$ Deduct Wallet $\rightarrow$ Confirm Order).
- **Hiện trạng**: ❌ CHƯA CÓ trong repo Gateway — đây là **quyết định phạm vi backend**. `Nexus.Gateway.csproj` có **0 package** EF Core / MassTransit / RabbitMQ; Gateway không có DB (0 `DbSet`). Saga thật chưa được triển khai ở bất kỳ service nào; gateway chỉ có `HealthProber/SagaRollbackPublisher.cs` **chỉ ghi log** (xem ADR 11).
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
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - Tầng 1 ✅ CÓ — `Common/ValidationHelpers.cs` (`EnsureRequired`, `EnsureGuid`, `EnsurePositive`), `Common/MoneyPrecision.cs`, `PaginationRequest.EnsureValid()`, `RequestTimeouts` 65s (`Extensions/CoreServicesExtension.cs:93-97`). ⚠️ `Idempotency-Key` chỉ được **chuyển tiếp** xuống gRPC metadata (`Dispatching/GrpcServiceCaller.cs:62`), **chưa** validate định dạng UUID như đặc tả.
  - Tầng 2/3 ❌ CHƯA CÓ — thuộc backend service; gateway không có `FluentValidation` package và không kiểm tra nghiệp vụ.
  - ⚠️ Vi phạm Nguyên tắc 5: 3 chuỗi thông báo lỗi tiếng Việt còn tồn tại tại `Common/ValidationHelpers.cs:23,29` và `Common/MoneyPrecision.cs:18`.
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
     - **SSL/TLS Termination**: Không cấu hình bên trong C# Gateway. Đây là trách nhiệm của lớp Ingress Proxy biên mạng (Cloudflare, AWS ALB, Nginx). Gateway chỉ chạy HTTP nội bộ tại cổng `8080`.
     - **Identity Provider (IdP) & Context Propagation**: Gateway đảm nhiệm xác thực JWT Bearer token ở cổng vào. Khi gọi gRPC xuống các microservices, Gateway tự động giải mã và gắn thông tin định danh vào gRPC Metadata:
       - `x-user-id`: User ID định danh (`sub` claim).
       - `x-user-roles`: Danh sách vai trò (`role` claims), cách nhau bởi dấu phẩy.
       - `x-user-email`: Email người dùng.
       - `x-trace-id`: Trace ID nhất quán của request.
     - **Lợi ích**: Các microservices con **không cần giải mã lại JWT**, giảm thiểu tính toán CPU lặp lại và chỉ cần lấy thông tin user trực tiếp từ `ServerCallContext.RequestHeaders`.
  5. **Quy tắc bắt buộc khi tạo Microservice mới**:
     - Mọi microservice mới khi đưa vào hệ sinh thái Nexus **bắt buộc** phải gọi hàm khởi tạo observability của riêng nó ngay trong `Program.cs` (gateway: `builder.Services.AddGatewayTelemetry(builder.Configuration);` — `Program.cs:17`, `Extensions/TelemetryExtension.cs:11`).
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - ✅ CÓ trong gateway: `AddGatewayTelemetry` (`Program.cs:17`) bật `AddAspNetCoreInstrumentation()`, `AddHttpClientInstrumentation()`, `AddSource(BackendDispatcher.ActivityName)`, `AddSource("Grpc.Net.Client")`; metrics `AddMeter("Microsoft.AspNetCore.RateLimiting")`; exporter OTLP chỉ bật khi có `OTEL_EXPORTER_OTLP_ENDPOINT`/`Otel:Endpoint`; resource service name `nexus-gateway`.
  - ⚠️ **Không** có instrumentation EF Core / Redis / MassTransit ở gateway (không có package tương ứng) — câu "tự động bắt vết EF Core SQL, Redis, MassTransit" chỉ đúng nếu service đó tự cài package. 6/8 backend service có config OTel nhưng thiếu package (xem audit hệ thống).
  - ❌ CHƯA CÓ: Tempo/Grafana **không** nằm trong `docker-compose.yml` của repo này — compose chỉ có service `gateway`; Tempo 4317/4318 và Grafana 3000 phải chạy ở stack ngoài (giai đoạn setup).
  - ⚠️ Header danh tính: metadata `x-user-id`/`x-user-roles`/`x-user-email`/`x-trace-id` chỉ được gắn khi đi qua `GrpcServiceCaller` và chỉ với client `users`; 0 backend nào đọc `x-trace-id`.
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
  7. **Controller Quản Trị (Controller thứ 15)**:
     - `HealthProberController` (`/api/v1/gateway/prober/...`) hỗ trợ xem trạng thái, bật/tắt từng daemon, bulk toggle và **API kiểm tra sức khỏe thủ công tức thì (Manual Check on demand)**.
- **Hiện trạng**: ⚠️ MỘT PHẦN.
  - ✅ CÓ: `HealthProberManager` (`HealthProber/HealthProberManager.cs`, mặc định mọi service `Disabled` tại `:53,:85,:158`), state machine enum `ProberRunStatus` = `Disabled, Starting, Healthy, Degraded, Unhealthy, Recovering`, 4 tham số đúng trong `appsettings.json` (`IntervalSeconds 60`, `TimeoutSeconds 30`, `FailureThreshold 5`, `StartPeriodSeconds 60`), log sự cố xoay file qua `FileRollingHealthIncidentLogger` (`logDirectory "logs/health"`, `maxLinesPerLogFile 1000`), 5 action quản trị trong `HealthProberController`.
  - ✅ **Circuit Breaker Fast-Fail**: `Dispatching/GatewayDispatcher.cs:103-113` và `:164-176` kiểm tra `_healthProberManager.IsServiceAvailable(service)` trước khi gọi gRPC, trả `503` + `errorCode: "DOWNSTREAM_UNHEALTHY"` khi service `Unhealthy`/`Recovering` — chỉ áp dụng nhánh `Mode: Real` (nhánh Mock không đi qua bước này).
  - ❌ **Saga Rollback**: `HealthProber/SagaRollbackPublisher.cs` **chỉ ghi warning log** và trả `Task.CompletedTask`; có comment `// Khi có MassTransit IBus...`. **Không có** publish lên RabbitMQ, gateway không có package MassTransit.
  - ⚠️ **Docker Auto-Recovery**: `HealthProber/DockerContainerRecoveryService.cs` shell-out `docker restart <container>` bằng `ProcessStartInfo`, kiểm tra `/var/run/docker.sock`. Compose **có** mount socket (`/var/run/docker.sock:/var/run/docker.sock`) nhưng image `mcr.microsoft.com/dotnet/aspnet:8.0` **không có docker CLI** ⇒ trên container thực tế cơ chế này **không chạy được**; giới hạn 3 lần (`MaxAutoRecoverAttempts`) và `AutoRecoverContainer: true` là cấu hình, không phải bằng chứng hoạt động.
  - ⚠️ **Batching log**: `Logging/GatewayBatchLogSink.cs` POST tới `{RemoteEndpoint}/api/v1/log/batch` (`http://localhost:8004`) nhưng **endpoint đó không tồn tại** — `Log.Api/Program.cs:66-67` chỉ map `/health` và `/`; sink bỏ qua response và `catch { }` rỗng nên log batch bị **mất im lặng**.
  - ⚠️ `logs/batches/batch-20261022-*.json` (9 file) đang bị **commit vào git** — cần bỏ track (GW-09).