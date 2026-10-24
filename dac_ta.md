# ĐẶC TẢ KỸ THUẬT & KIẾN TRÚC TOÀN DIỆN API GATEWAY (.NET 8)
## HỆ SINH THÁI MICROSERVICES NEXUS
> **Trạng thái triển khai (2026-10-24):** ⚠️ Một phần — 15 file controller nhưng chỉ **4 controller có action (16 action)**, 11 controller là stub 10 dòng; **1/8** gRPC client (`users`); `Contracts/shop.proto` chỉ có `UsersApi` 5 rpc; 62 test attribute trên 19 file.
> **Nguồn sự thật:** code trong repo này (`file:line` ghi kèm trong từng bảng).
> **Cập nhật lần cuối:** 2026-10-24.
> **Dành cho độc giả**: Tài liệu này đóng vai trò là bản đặc tả kỹ thuật hoàn chỉnh (Master Technical Specification). Bất kỳ ai — từ lập trình viên Frontend, Backend, DevOps, Solution Architect đến các AI Coding Agent — khi đọc tài liệu này đều sẽ hiểu rõ: **Gateway làm được những gì, cấu tạo gồm những thành phần nào, nguyên lý hoạt động chi tiết của từng tính năng (Mock, CORS, Rate Limit, Auth, gRPC Translation...), và cách vận hành trong thực tế.**
---
# MỤC LỤC TỔNG QUAN
1. [Tổng Quan Vai Trò & Năng Lực Cốt Lõi Của ApiGateway](#1-tổng-quan-vai-trò--năng-lực-cốt-lõi-của-apigateway)
2. [Kiến Trúc 3 Khối Độc Lập (3-Tier Decoupling Architecture)](#2-kiến-trúc-3-khối-độc-lập-3-tier-decoupling-architecture)
3. [Giải Thích Chi Tiết Từng Tính Năng & Cơ Chế Hoạt Động](#3-giải-thích-chi-tiết-từng-tính-năng--cơ-chế-hoạt-động)
   - [3.1. Tính Năng Mock Engine & Seed Fixtures (Phát Triển Độc Lập)](#31-tính-năng-mock-engine--seed-fixtures-phát-triển-độc-lập)
   - [3.2. Tính Năng Chuyển Đổi Trạng Thái Gateway (Standard vs Bypass Mode)](#32-tính-năng-chuyển-đổi-trạng-thái-gateway-standard-vs-bypass-mode)
   - [3.3. Tính Năng Chuyển Đổi Mock $\leftrightarrow$ Real Cho Từng Service (Hot-Reload)](#33-tính-năng-chuyển-đổi-mock--real-cho-từng-service-hot-reload)
   - [3.4. Tính Năng CORS (Cross-Origin Resource Sharing)](#34-tính-năng-cors-cross-origin-resource-sharing)
   - [3.5. Tính Năng Distributed Rate Limiting (Giới Hạn Tần Suất Gọi Qua Redis)](#35-tính-năng-distributed-rate-limiting-giới-hạn-tần-suất-gọi-qua-redis)
   - [3.6. Tính Năng Bảo Mật, JWT Authentication & Phân Quyền RBAC](#36-tính-năng-bảo-mật-jwt-authentication--phân-quyền-rbac)
   - [3.7. Tính Năng Chuyển Đổi Giao Thức REST $\rightarrow$ gRPC (Protocol Translation)](#37-tính-năng-chuyển-đổi-giao-thức-rest--grpc-protocol-translation)
   - [3.8. Tính Năng Bảo Vệ Kestrel Server & Request Timeouts](#38-tính-năng-bảo-vệ-kestrel-server--request-timeouts)
   - [3.9. Tính Năng Xử Lý Lỗi Tập Trung & Chuẩn Hóa Phản Hồi (RFC 9110 / RFC 6585)](#39-tính-năng-xử-lý-lỗi-tập-trung--chuẩn-hóa-phản-hồi-rfc-9110--rfc-6585)
   - [3.10. Tính Năng Phân Tán Trace & Giám Sát (OpenTelemetry Tracing)](#310-tính-năng-phân-tán-trace--giám-sát-opentelemetry-tracing)
   - [3.11. Chiến Lược Phòng Thủ Xác Thực Dữ Liệu 3 Tầng (3-Tier Validation Defense-in-Depth)](#311-chiến-lược-phòng-thủ-xác-thực-dữ-liệu-3-tầng-3-tier-validation-defense-in-depth)
   - [3.12. Tính Năng Health Prober Daemon, Giám Sát Sức Khỏe & Tự Động Hồi Phục (Self-Healing)](#312-tính-năng-health-prober-daemon-giám-sát-sức-khỏe--tự-động-hồi-phục-self-healing)
4. [Bản Đồ Cấu Trúc Mã Nguồn & Phân Bổ Trách Nhiệm](#4-bản-đồ-cấu-trúc-mã-nguồn--phân-bổ-trách-nhiệm)
5. [Bảng Tổng Hợp 15 Controllers & Danh Mục 17 Nhóm Route](#5-bảng-tổng-hợp-15-controllers--danh-mục-17-nhóm-route)
6. [Quy Trình 3 Bước Thêm API Mới Dành Cho Developer](#6-quy-trình-3-bước-thêm-api-mới-dành-cho-developer)
7. [Bảo Đảm Chất Lượng & Kiểm Thử Tự Động (62 Test Attribute / 19 File)](#7-bảo-đảm-chất-lượng--kiểm-thử-tự-động-62-test-attribute--19-file)
---
# 1. TỔNG QUAN VAI TRÒ & NĂNG LỰC CỐT LÕI CỦA APIGATEWAY
Trong kiến trúc Microservices của hệ sinh thái Nexus, **ApiGateway** đóng vai trò là **Edge Reverse Proxy** (Cổng vào biên duy nhất) đứng phía trước toàn bộ 8 Microservices nội bộ:
```
[ Khách Hàng: Web Browser / Next.js / Mobile App ]
                       │
                       ▼ (HTTP/REST JSON: /api/v1/... - Cổng 8000)
           ┌────────────────────────┐
           │       ApiGateway       │ ──(RateLimit Cache)──▶ [ Redis : 6379 ]
           └────────────────────────┘
                       │
                       ├── (gRPC Channel - Protobuf v3 - Mạng Nội Bộ Tốc Độ Cao)
                       ▼
   ┌───────────────┬─┴─────────────┬───────────────┬───────────────┐
   ▼               ▼               ▼               ▼               ▼
UsersService    CatalogService  CommerceService  LogService     AuctionService ... (+ Fulfillment, Notification, Wallet)
(Cổng 8001)     (Cổng 8002)     (Cổng 8003)      (Cổng 8004)    (Cổng 8005)        (Cổng 8006, 8007, 8008)
```
### 1.1. Tại sao hệ thống cần ApiGateway?
Nếu không có Gateway, các ứng dụng Client (Frontend, Mobile) sẽ phải kết nối trực tiếp đến từng cổng của 8 service khác nhau. Điều này gây ra:
- **Lỗ hổng bảo mật**: Lộ toàn bộ sơ đồ mạng và cổng nội bộ ra Internet.
- **Rối loạn xác thực**: Từng service backend phải tự cài đặt bộ kiểm tra JWT, CORS và Rate Limiting lặp đi lặp lại.
- **Bất đồng bộ giao thức**: Frontend chỉ hiểu HTTP/REST JSON, trong khi các microservices backend giao tiếp hiệu năng cao bằng gRPC Protobuf v3.
- **Khó khăn khi phát triển giao diện (FE bị block)**: Nếu Backend chưa code xong hoặc chưa dựng database, Frontend hoàn toàn bị tê liệt vì không có API để test.
### 1.2. Những gì ApiGateway làm được (Bảng Tóm Tắt Năng Lực):
1. **Cổng vào tập trung duy nhất (Single Point of Entry)** — ✅ CÓ: mọi request ngoại vi vào cổng `8000` với tiền tố `/api/v1/...` (`Properties/launchSettings.json`, `Dockerfile EXPOSE 8000`). ⚠️ Nhưng tiền tố **không** đồng nhất `/api/v1/{service}/...`: route thật gồm `/api/v1/auth/*`, `/api/v1/users/me`, `/api/v1/gateway/*`, `/api/v1/mock-engine/*`, `/api/v1/catalog/products`, `/api/v1/commerce/orders`, `/api/v1/wallet`...
2. **Chuyển đổi giao thức thông minh (Protocol Translation)** — ⚠️ MỘT PHẦN: cơ chế REST → gRPC có thật (`Dispatching/GrpcServiceCaller.cs`, `Dispatching/GatewayDispatcher.cs`), nhưng **chỉ 1/8 service** có client (`UsersApi.UsersApiClient` đăng ký bởi `Extensions/GrpcClientsExtension.cs`). 7 service còn lại chưa có rpc trong `Contracts/shop.proto` nên không thể gọi thật.
3. **Môi trường phát triển độc lập (Mock Engine tích hợp)** — ⚠️ MỘT PHẦN: có **hai** cơ chế mock chạy song song: (a) seed fixture đọc theo từng request (`Dispatching/MockDataMapper.cs`) với **9 file** trong `Seeds/` chỉ cho `auth` (4) và `users` (5); (b) declarative mock `Mocks/*.mock.json` (**6 file**) do `MockEngine/MockEngineMiddleware.cs` phục vụ khi route **không** khớp action nào. **Không** có seed cho `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`.
4. **Chuyển đổi trạng thái Gateway linh hoạt** — ✅ CÓ: cấu hình `Gateway:AuthMode` với giá trị `Enforce` / `Standard`; `Bypass` (và `Mock`, `MockBypass`) **chỉ hợp lệ ở Development** (`GatewayOptions.IsValid`). Tên cũ `GatewayMode` chỉ còn là alias đọc-legacy. Ở `Bypass`, Frontend test giao diện không cần đăng nhập, trừ control-plane (xem mục 3.2).
5. **Hot-Reload Mock $\leftrightarrow$ Real theo từng service** — ⚠️ MỘT PHẦN: đổi mode từng service qua `PUT /api/v1/gateway/modes/{service}` hoặc sửa `gateway.runtime.json` (nạp với `reloadOnChange: true`) đều có hiệu lực tức thì, **nhưng** bật `catalog: Real` hiện không gọi được vì chưa có client/rpc Catalog.
6. **Bảo mật và phân quyền biên (Edge Security & RBAC)** — ✅ CÓ: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10`) + 6 policy `GatewayPolicies.{Authenticated, Customer, Seller, Staff, Admin, GatewayAdmin}` (`Security/SecurityConfiguration.cs:156-161`). ⚠️ Không service nào **phát** JWT (`rpc Login` mới chỉ có ở phía client).
7. **Bảo vệ hệ thống bằng Rate Limiting phân tán** — ✅ CÓ: `Middleware/RedisRateLimitMiddleware.cs` + StackExchange.Redis 2.8.24; policy thật: `api`, `auth-strict`, `auth-login`, `user-frequent`.
8. **Chính sách CORS chuẩn mực** — ✅ CÓ: `GatewayOptions.CorsOrigins/CorsMethods/CorsHeaders/CorsAllowCredentials`, cấu hình trong `appsettings.json`.
9. **Chống treo hệ thống và tràn bộ nhớ** — ✅ CÓ: `Program.cs:9` `MaxRequestBodySize = 64 * 1024`; `Extensions/CoreServicesExtension.cs:93-97` RequestTimeout 65s.
10. **Chuẩn hóa phản hồi & lỗi quốc tế** — ✅ CÓ: `Responses/ApiResponse.cs` + `Responses/ApiErrorResponse.cs`; `Middleware/GatewayExceptionHandler.cs:61-80` map 14 nhánh lỗi sang `errorCode` SCREAMING_SNAKE_CASE.
11. **Quan sát phân tán (Observability)** — ✅ CÓ: OpenTelemetry 1.18.0 (`Extensions/TelemetryExtension.cs`, gọi tại `Program.cs:17`), gắn `x-trace-id` xuống upstream (`Dispatching/GrpcServiceCaller.cs:68`). ⚠️ Không có instrumentation EF Core / Redis / MassTransit; không có Tempo/Grafana trong `docker-compose.yml` của repo này.
---
# 2. KIẾN TRÚC 3 KHỐI ĐỘC LẬP (3-TIER DECOUPLING ARCHITECTURE)
Để khắc phục hoàn toàn tình trạng code bị dồn cục, khó bảo trì, hệ thống Gateway được chia tách thành **3 khối độc lập** với ranh giới trách nhiệm cực kỳ rành mạch:
```
[ Client Request: /api/v1/... ]
                │
                ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 1. KHỐI ĐẦU VÀO (INPUT TIER) — Controllers/ & Dtos/                    │
│    - Gồm 15 Controllers kế thừa [ApiController] ControllerBase,      │
│      nhưng CHỈ 4 controller có action (16 action); 11 file còn lại   │
│      là stub 10 dòng chứa 1 dòng TODO.                               │
│    - Tiếp nhận DTOs: hiện chỉ có 4 thư mục Dtos/{Auth,Users,           │
│      GatewayControl,HealthProber}.                                     │
│    - Model binding kiểm tra định dạng; action kiểm tra input đặc thù.    │
│    - Không chứa if/else Mock/Real; gọi DispatchAsync/DispatchPagedAsync. │
│    - Validation riêng có thể throw GatewayInputException.              │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 2. KHỐI PHẦN GIỮA (DISPATCHING CORE) — Dispatching/                    │
│    - GatewayRequestResolution: snapshot endpoint/mode theo request      │
│      trước authentication; ModeEvaluator đọc lại snapshot cho dispatch.│
│    - MockDataMapper: "Người đọc Mock" — Đọc file Seed JSON, thẩm định  │
│      cú pháp qua Protobuf MessageParser.ParseJson() nghiêm ngặt.       │
│    - GrpcServiceCaller: "Người gọi Backend" — Tự động forward Metadata │
│      (Token, Idempotency-Key, TraceId) và gọi gRPC typed client.       │
│    - GatewayDispatcher: "Bộ điều phối tổng" — Nhận lệnh từ Controller, │
│      phối hợp 3 người trên và chuyển data gốc sang Khối Đầu Ra.        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ 3. KHỐI ĐẦU RA (OUTPUT TIER) — Responses/ & Middleware/                │
│    - ApiResponseFactory: Không quan tâm dữ liệu từ Mock hay Real, tự   │
│      động bọc kết quả vào ApiResponse<T> hoặc ApiResponse<PagedData<T>>,│
│      tự động cấp mã TraceId và ISO timestamp.                          │
│    - GatewayExceptionHandler: bắt input, gRPC, timeout và lỗi nội bộ.    │
│      Model binding, auth và rate-limit có hook dùng cùng error schema.   │
└────────────────────────────────────────────────────────────────────────┘
                │
                ▼
[ Client Response JSON ]
```
---
# 3. GIẢI THÍCH CHI TIẾT TỪNG TÍNH NĂNG & CƠ CHẾ HOẠT ĐỘNG
Dưới đây là phần mô tả chuyên sâu cách thức hoạt động của từng tính năng bên trong Gateway:
---
## 3.1. Tính Năng Mock Engine & Seed Fixtures (Phát Triển Độc Lập)
### Vấn đề thực tế:
Trong các dự án Microservices, việc Frontend phải chờ Backend code xong API, tạo database và dựng môi trường mới có thể bắt đầu làm việc là nguyên nhân hàng đầu gây chậm trễ tiến độ dự án.
### Giải pháp của Gateway:
Gateway tích hợp sẵn một **Hệ Thống Mock Kép (Dual Mock Engine)**:
```
                          ┌────────────────────────┐
                          │   Client Gửi Request   │
                          └───────────┬────────────┘
                                      │
                         [Kiểm tra Effective Mode]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
           [ Chế độ MOCK ]                        [ Chế độ REAL ]
                   │                                     │
     Đọc file Seeds/{service}/*.json           Tự động gọi gRPC Channel
                   │                           tới Backend Microservice
                   ▼                                     │
   Ép qua Protobuf MessageParser                         ▼
   Đảm bảo đúng schema 100%                    Nhận kết quả Protobuf
                   │                                     │
                   └──────────────────┬──────────────────┘
                                      │
                                      ▼
                      Bọc vào ApiResponseFactory chuẩn
```
1. **Static Protobuf Fixtures (`Seeds/{service}/{operation}.json`)**:
   - Khi một service ở chế độ Mock, `MockDataMapper` sẽ đọc file JSON tương ứng. ⚠️ **Hiện chỉ có seed cho `auth` và `users`** (ví dụ thật: `Seeds/auth/login.json`, `Seeds/users/profile.json`); `Seeds/catalog/products.json` **không tồn tại** (thư mục `Seeds/products/` chỉ có `README.md`).
   - **Cơ chế thẩm định hợp đồng nghiêm ngặt (Strict Contract Verification)**:
     ```csharp
     var protoMessage = parser.ParseJson(json);
     ```
     Dữ liệu JSON mẫu bắt buộc phải được ép qua trình phân tích của Protobuf. Nếu dev viết sai kiểu dữ liệu (ví dụ: gõ string vào trường số nguyên), hệ thống sẽ báo lỗi khi parse fixture. Proto3 cho phép một số trường vắng mặt nhận giá trị mặc định; parse hợp lệ không bảo đảm fixture đúng nghiệp vụ hoặc backend thật. Hãy có test contract/mock và test tích hợp backend.
   - **Giả lập độ trễ mạng (`MockDelayMs`)**: Có thể cấu hình giả lập độ trễ mạng (ví dụ 500ms) để Frontend kiểm tra hiệu ứng Loading / Spinner của giao diện.
   - **Giả lập mã lỗi (`MockStatus`)**: Có thể cấu hình trả về mã lỗi 503 để Frontend kiểm tra màn hình hiển thị lỗi mạng.
2. **Dynamic Declarative Mock Engine (`*.mock.json`)**:
   - Cho phép dev tạo nhanh các API REST mock động chỉ bằng cách tạo một file JSON trong thư mục `Nexus.Gateway/Mocks/**/*.mock.json` mà **không cần viết bất kỳ dòng code C# nào**.
   - Hỗ trợ tham số URL động (ví dụ: `/api/v1/catalog/products/{id}`).
   - Tự động quét file và nạp nóng (Hot-Reload qua `FileSystemWatcher`).
   - Tự động sinh tài liệu trên Swagger UI qua `MockEngineDocumentFilter`.
---
## 3.2. Tính Năng Chuyển Đổi Trạng Thái Auth (Enforce vs Bypass Mode)
> ⚠️ **Đính chính thuật ngữ**: trục cấu hình thật là `Gateway:AuthMode` (`Nexus.Gateway/GatewayOptions.cs:38`) với các giá trị `Enforce` (mặc định, tương đương tên cũ `Standard`), `Standard`, và `Bypass`/`Mock`/`MockBypass` (**chỉ Development**, `GatewayOptions.IsValid`). `GatewayMode` (`GatewayOptions.cs:42`) chỉ còn là **alias đọc-legacy** — bản cũ gọi tên "GatewayMode" cho cả trục là sai.
### Vấn đề thực tế:
Các API kinh doanh như xem giỏ hàng (`/api/v1/commerce/cart`), thông tin cá nhân (`/api/v1/users/me`), hay lịch sử đơn hàng đều yêu cầu đăng nhập và có token JWT mang vai trò tương ứng. Khi Frontend dev đang code giao diện, việc token bị hết hạn hoặc phải login đi login lại để lấy token test rất mất thời gian.
### Giải pháp của Gateway:
Gateway cung cấp **Cơ chế 2 Trạng Thái Auth (`Gateway:AuthMode`)**:
```
                       ┌──────────────────────────────┐
                       │  Client Request Đến Gateway  │
                       └──────────────┬───────────────┘
                                      │
                         [Kiểm tra Gateway:AuthMode]
                                      │
                   ┌──────────────────┴──────────────────┐
                   ▼                                     ▼
   [ AuthMode: "Enforce" (Mặc định) ]        [ AuthMode: "Bypass" (Development) ]
                   │                                     │
       Bắt buộc phải có JWT Token hợp lệ      Nếu gọi API Mock: Bỏ qua kiểm tra auth,
       trên header Authorization: Bearer.     tự động gán danh tính giả lập tương ứng.
       Không có token -> 401 Unauthorized.    ────────────────────────────────────────
       Sai quyền -> 403 Forbidden.            Ngoại lệ: /api/v1/gateway/... và
                                              /api/v1/mock-engine/... vẫn bắt buộc
                                              Token Admin thật.
```
1. **Trạng Thái Mặc Định (`"Enforce"`, tên cũ `"Standard"` — tương đương)**:
   - Mọi quy tắc an ninh hoạt động bình thường và nghiêm ngặt.
   - Chưa đăng nhập (không gửi header Authorization) $\rightarrow$ Chặn với mã `401 Unauthorized`.
   - Đăng nhập qua `POST /api/v1/auth/login` lấy token gửi vào request $\rightarrow$ Thành công `200 OK`. ⚠️ Ở chế độ `Real` luồng này còn phụ thuộc `rpc Login` phía User service (chưa implement).
   - Token không đủ quyền (ví dụ buyer gọi endpoint staff) $\rightarrow$ Chặn với mã `403 Forbidden`.
2. **Trạng Thái Mock Bypass (`"Bypass"` — Dành riêng cho Development)**:
   - Khi bật `Bypass`, lập trình viên có thể click test trên Swagger UI hoặc gọi từ code Frontend tới **API Mock** mà không cần đăng nhập hay mang token.
   - Middleware `Security/MockSecurityAdapterMiddleware.cs` tự động phát hiện quyền hạn yêu cầu của API và cấp phát danh tính giả lập `ClaimsPrincipal` tương ứng (ví dụ: gắn claim vai trò khi gọi route thuộc nhóm quản trị — ví dụ route stub thật `api/v1/catalog/inventory`; route `/api/v1/seller/inventory` **không tồn tại** trong repo này).
   - **Rào chắn an toàn tối thượng (`[DisableMockAuthorizationBypass]`)**: Các API điều khiển hệ thống (`/api/v1/gateway/...` và `/api/v1/mock-engine/...`) tuyệt đối không thể bypass, bắt buộc phải có token quản trị viên thật với role `gateway-admin` — xem `GatewayControlController.cs:16`, `HealthProberController.cs:18`.
   - Trên môi trường `Production`, chế độ `Bypass`/`Mock`/`MockBypass` bị **vô hiệu hóa hoàn toàn**: `GatewayOptions.IsValid(development: false)` từ chối cấu hình (`ValidateOnStart`), và `appsettings.json` (production) đặt `AuthMode: "Enforce"`, `AllowMockTokenAuth: false`, `AllowAnonymousMockAuthInDevelopment: false`.
---
## 3.3. Tính Năng Chuyển Đổi Mock $\leftrightarrow$ Real Cho Từng Service (Hot-Reload)
### Vấn đề thực tế:
Khi một lập trình viên Backend hoàn thành xong `CatalogService:8002`, họ muốn kiểm thử tích hợp thực tế với Gateway, nhưng các service khác (`UserService`, `CommerceService`) vẫn chưa hoàn thành hoặc chưa bật.
### Giải pháp của Gateway:
Gateway cho phép cô lập trạng thái Mock/Real độc lập cho từng service trong số 8 services (⚠️ **chỉ `users` thực thi được `Real`** vì là service duy nhất có gRPC client; 7 service còn lại đổi sang `Real` sẽ không có client để gọi):
| Cách Chuyển Đổi | Thao Tác Thực Hiện | Tác Động |
|:---|:---|:---|
| **Qua File Runtime (Hot-Reload)** | Sửa file [gateway.runtime.json](Nexus.Gateway/gateway.runtime.json):<br>`"catalog": { "Mode": "Real", "Address": "http://localhost:8002" }` | Có hiệu lực tức thì cho toàn bộ request tiếp theo, **KHÔNG CẦN restart Gateway**. |
| **Qua API Quản Trị** | Gửi request `PUT /api/v1/gateway/modes/catalog`<br>Body: `{"mode": "Real"}` | Gateway tự động cập nhật bộ nhớ và ghi đè vào file runtime. |
| **Qua Request Header (Dev)** | Gửi header HTTP trên request riêng lẻ:<br>`X-Service-Mode-catalog: Real` | Chỉ ép buộc chuyển sang Real cho duy nhất request đó, không ảnh hưởng tới người khác. |
---
## 3.4. Tính Năng CORS (Cross-Origin Resource Sharing)
### Vấn đề thực tế:
Khi ứng dụng Frontend chạy tại `http://localhost:3000` (React/Next.js) gọi API sang Gateway chạy tại `http://localhost:8000`, trình duyệt web sẽ tự động chặn request theo chính sách bảo mật Same-Origin Policy nếu Gateway không cấu hình CORS chuẩn.
### Cách Gateway xử lý CORS:
Triển khai trong [EdgeMiddlewareConfiguration.cs](Nexus.Gateway/Security/EdgeMiddlewareConfiguration.cs):
- **Cấu hình nguồn cho phép (`CorsOrigins`)**: Đọc từ cấu hình `Gateway:CorsOrigins` (hỗ trợ nhiều origin như `http://localhost:3000`, `http://localhost:5173`).
- **Phương thức cho phép (`CorsMethods`)**: Cho phép đầy đủ các động từ RESTful: `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `OPTIONS`.
- **Headers cho phép (`CorsHeaders`)**: Hỗ trợ toàn bộ header chuẩn và các custom header như `Authorization`, `Content-Type`, `Idempotency-Key`, `X-Trace-Id`, `X-Gateway-Mode`, `X-Service-Mode-*`.
- **Exposed Headers**: Công khai các header phản hồi quan trọng để JavaScript trên trình duyệt có thể đọc được: `X-Trace-Id`, `X-Gateway-Mode`, `Retry-After`.
- **Hỗ trợ Preflight (OPTIONS Request)**: Tự động trả về HTTP `204 No Content` cho các preflight request của trình duyệt trước khi thực thi lệnh ghi dữ liệu.
---
## 3.5. Tính Năng Distributed Rate Limiting (Giới Hạn Tần Suất Gọi Qua Redis)
### Vấn đề thực tế:
Nếu một Client gửi liên tục hàng nghìn request mỗi giây (do lỗi lặp vô tận từ Frontend, bị kẻ xấu tấn công DoS, hoặc quét dữ liệu), hệ thống backend sẽ nhanh chóng bị nghẽn mạng và sập cơ sở dữ liệu.
### Cách Gateway xử lý Rate Limiting:
✅ **CÓ** — triển khai qua [RedisRateLimitMiddleware.cs](Nexus.Gateway/Middleware/RedisRateLimitMiddleware.cs) (StackExchange.Redis 2.8.24, Lua `INCR` + `EXPIRE` ⇒ fixed window):
| Thành phần | Giá trị thật | Bằng chứng |
|:---|:---|:---|
| Policy đang cấu hình | `api`, `auth-strict`, `auth-login`, `user-frequent` | `appsettings.json:39,43,47` |
| Partition | `sub` claim, fallback `RemoteIpAddress`, cuối cùng `anonymous` | `RedisRateLimitMiddleware.cs:67-68` |
| Ngưỡng mặc định | 100 request / 60 giây | `GatewayOptions.PermitLimit/WindowSeconds` |
| Phản hồi khi vượt | 429 + `Retry-After` + `errorCode: RATE_LIMIT_EXCEEDED` + type RFC 6585 §4 | `RedisRateLimitMiddleware.cs:110-118` |
| Fail-open | Chỉ khi Development **và** `RedisFailOpenInDevelopment = true` | `:49,80,86` |
```
Client Request
      │
      ▼
[ RedisRateLimitMiddleware ] ──(Kiểm tra Counter)──▶ [ Redis Cache ]
      │
      ├── Đạt giới hạn cho phép?
      │     ├── [CÒN LƯỢT] ──▶ Chuyển request tới Controller xử lý.
      │     └── [VƯỢT NGƯỠNG] ─▶ Chặn ngay lập tức tại Gateway:
      │                           - Trả về mã HTTP 429 Too Many Requests
      │                           - Đính kèm Header: Retry-After: <số giây>
      │                           - Trả JSON chuẩn ApiErrorResponse RFC 6585
```
- **Phân vùng giới hạn (Partitioning)**:
  - Nếu đã đăng nhập: Giới hạn theo mã định danh người dùng (`sub` claim trong JWT).
  - Nếu là khách vãng lai: Giới hạn theo địa chỉ IP thực của Client (`RemoteIpAddress`).
- **Thuật toán Sliding/Fixed Window**: Đếm số lượt gọi trong một cửa sổ thời gian (mặc định: tối đa `100` requests trong `60` giây).
- **Chính sách Fail-Open ở Development**: Nếu trong môi trường lập trình local mà Redis chưa được bật hoặc gặp sự cố kết nối, Gateway sẽ tự động **cho request đi qua và ghi log cảnh báo**, tuyệt đối không chặn đứng công việc của lập trình viên.
---
## 3.6. Tính Năng Bảo Mật, JWT Authentication & Phân Quyền RBAC
### Quy trình xác thực JWT Bearer:
1. Client gửi request mang header `Authorization: Bearer <token>`.
2. Gateway kiểm tra chữ ký token bằng thuật toán HMAC-SHA256 với Secret Key bí mật (chuỗi an toàn tối thiểu 32 ký tự).
3. Gateway kiểm định tính hợp lệ của các trường: Nhà phát hành (`iss`), đối tượng phục vụ (`aud`), và thời hạn sống (`exp`).
4. Nếu hợp lệ, Gateway nạp thông tin danh tính thành `ClaimsPrincipal` và chuyển tiếp header `Authorization` nguyên vẹn vào gRPC metadata khi gọi xuống các microservices backend.
### Ma trận phân quyền RBAC (6 policy thật):
Hệ thống áp dụng mô hình phân quyền có tính kế thừa:
```
[ anonymous ] (Chưa đăng nhập)
      │
      ▼
   [ buyer ] (Khách hàng đã đăng ký)
      │
      ├───────────────────────┐
      ▼                       ▼
  [ seller ]              [ staff ]
(Người bán hàng)      (Nhân viên vận hành)
      │                       │
      └───────────┬───────────┘
                  ▼
              [ admin ] (Quản trị viên toàn sàn)
                  │
                  ▼
          [ gateway-admin ] (Quản trị viên kỹ thuật Gateway)
```
Các **policy thật** đăng ký trong `Security/SecurityConfiguration.cs:156-161` — đối chiếu với `Security/GatewayAuthorization.cs:5-11`:
| Policy | Điều kiện thật trong code | Ý nghĩa nghiệp vụ (mục tiêu) | Trạng thái |
|:---|:---|:---|:---|
| `authenticated` | `RequireAuthenticatedUser().RequireClaim("sub")` | Mọi user đã đăng nhập — dùng cho `GET /api/v1/users/me` | ✅ CÓ — đang dùng |
| `customer` | `RequireAuthenticatedUser().RequireClaim("sub")` | Buyer thao tác tài nguyên của chính mình | ✅ CÓ (policy) — ⚠️ chưa action nào dùng |
| `seller` | `RequireRole("seller", "admin")` | Người bán + admin | ✅ CÓ (policy) — ⚠️ chưa action nào dùng |
| `staff` | `RequireRole("staff", "admin")` | Nhân viên vận hành | ✅ CÓ (policy) — ⚠️ chưa action nào dùng |
| `admin` | `RequireRole("admin")` | Quản trị viên toàn sàn | ✅ CÓ (policy) — ⚠️ chưa action nào dùng |
| `gateway-admin` | `RequireRole("admin")` | Control-plane Gateways | ✅ CÓ — đang dùng (GatewayControl, HealthProber) |
> ⚠️ **Đính chính 2 điểm của bản cũ**:
> 1. `buyer` **không phải** một role/policy được kiểm tra — policy `customer` chỉ yêu cầu claim `sub`, bất kỳ ai đăng nhập đều qua. Tên `buyer` chỉ xuất hiện ở 2 chỗ: `Gateway:DefaultMockRole` (danh tính giả lập) và danh sách mock token trong `SecurityConfiguration.IsMockToken`.
> 2. `gateway-admin` là **tên policy**, không phải role JWT: quyền thực tế được kiểm bằng `RequireRole("admin")` — tức mọi token có role `admin` đều qua được control-plane, **không** có role `gateway-admin` riêng trong code.
>
> Ghi chú khác: `sub` là claim bắt buộc của policy `authenticated`; `SecurityConfiguration.cs:35` đặt `MapInboundClaims = false` và `:46` đặt `RoleClaimType = "role"` — token thiếu `sub` hoặc dùng tên claim role khác (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`) sẽ bị từ chối dù chữ ký hợp lệ.
---
## 3.7. Tính Năng Chuyển Đổi Giao Thức REST $\rightarrow$ gRPC (Protocol Translation)
### Tại sao lại dùng mô hình lai này?
- **Phía ngoài (Client $\rightarrow$ Gateway)**: Trình duyệt web và ứng dụng di động hỗ trợ tốt nhất với giao thức **RESTful JSON** (dễ debug, dễ tích hợp trên mọi nền tảng).
- **Phía trong (Gateway $\rightarrow$ Microservices)**: Giao tiếp giữa các server nội bộ cần tốc độ cao, độ trễ thấp và băng thông nhỏ, do đó **gRPC trên HTTP/2 nhị phân** là sự lựa chọn vượt trội.
### Cơ chế hoạt động (trên luồng DUY NHẤT đang chạy được: `users`):
Luồng thật, đã đối chiếu code — `POST /api/v1/auth/login`:
1. `AuthController.Login` (`AuthController.cs:106`) nhận body DTO, gọi `_client.LoginAsync(...)` với `UsersApi.UsersApiClient`.
2. `Extensions/GrpcClientsExtension.cs` là nơi **duy nhất** đăng ký typed client (`"users"`), nên chỉ service `users` có `GrpcServiceCaller` thật để gọi.
3. Khi service ở mode `Real`, `GrpcServiceCaller` gắn metadata `x-trace-id`, `x-user-id`, `x-user-roles`, `x-user-email`, `Idempotency-Key` (`GrpcServiceCaller.cs:62-89`) rồi gọi qua HTTP/2 tới địa chỉ trong `Gateway:Services:users:Address`.
4. Kết quả Protobuf được `GatewayDispatcher` chuyển thành JSON và `ApiResponseFactory` bọc vào `ApiResponse<T>`.
5. Khi service ở mode `Mock`, bước 2-4 được thay bằng `MockDataMapper` đọc seed `Seeds/auth/login.json` (hoặc `Seeds/users/login.json`).
> ❌ **Ví dụ Commerce trong bản cũ là đặc tả mục tiêu, chưa triển khai**: `CommerceController` không tồn tại (file thật là `Controllers/OrdersController.cs`, stub 0 action), `CreateOrderRequest`/`Order` không có trong `shop.proto`, và `Gateway:Services:commerce` hiện **không** có client để gọi. `POST /api/v1/commerce/orders` sẽ trả 404 (không action) trừ khi có `Mocks/` khai báo route tương ứng.
---
## 3.8. Tính Năng Bảo Vệ Kestrel Server & Request Timeouts
1. **Giới Hạn Kích Thước Body Request (64KB Limit)**:
   - Được cấu hình tại [Program.cs](Nexus.Gateway/Program.cs):
     ```csharp
     builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 64 * 1024);
     ```
   - Mục đích: Gateway chỉ phục vụ các API nghiệp vụ dữ liệu JSON thông thường. Việc chặn request vượt quá 64KB giúp ngăn chặn các đợt tấn công cố tình gửi file rác khổng lồ làm cạn kiệt RAM của Gateway.
2. **Áp Đặt Thời Gian Chờ Xử Lý (Request Timeout 65s)**:
   - Cấu hình qua middleware `RequestTimeouts`:
     ```csharp
     services.AddRequestTimeouts(o => o.DefaultPolicy = new RequestTimeoutPolicy
     {
         Timeout = TimeSpan.FromSeconds(65),
         TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
     });
     ```
   - Nếu một backend service bị treo hoặc database bị deadlock quá thời gian cho phép, Gateway sẽ chủ động cắt kết nối và trả về mã HTTP `504 Gateway Timeout`, không để kết nối bị treo vĩnh viễn gây cạn kiệt thread pool (chống tấn công Slowloris).
---
## 3.9. Tính Năng Xử Lý Lỗi Tập Trung & Chuẩn Hóa Phản Hồi (RFC 9110 / RFC 6585)
### Envelope Phản Hồi Thành Công Đơn Lẻ (`ApiResponse<T>`):
```json
{
  "success": true,
  "statusCode": 200,
  "message": "User retrieved successfully.",
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
### Envelope Phản Hồi Phân Trang (`ApiResponse<PagedData<T>>`):
```json
{
  "success": true,
  "statusCode": 200,
  "data": {
    "items": [
      { "id": "22222222-2222-2222-2222-000000000001", "name": "Bàn phím cơ", "price": "1250000.00" }
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
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
}
```
### Envelope Phản Hồi Thất Bại (`ApiErrorResponse`):
Được tạo qua `IApiResponseFactory` bởi exception handler hoặc hook tương ứng (MVC model binding, auth, rate-limit). Cấu trúc theo tinh thần Problem Details với `errorCode` và `errors` mở rộng; **không** lấy ví dụ `ApiResponse<T>.Error()` của dự án khác làm contract cho Gateway:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errorCode": "VAL_INVALID_INPUT",
  "detail": "Please refer to the errors property for additional details.",
  "errors": {
    "Email": ["The Email field is not a valid e-mail address."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "timestamp": "2026-10-07T08:25:00Z"
}
```
#### Bảng Ánh Xạ Mã Lỗi gRPC Sang HTTP Status Code:
| Mã Lỗi gRPC (`StatusCode`) | Mã Lỗi HTTP | Ý Nghĩa Nghiệp Vụ |
|:---|:---:|:---|
| `InvalidArgument` | **400 Bad Request** | Tham số đầu vào không hợp lệ hoặc thiếu trường bắt buộc. |
| `Unauthenticated` | **401 Unauthorized** | Chưa đăng nhập hoặc token không hợp lệ. |
| `PermissionDenied` | **403 Forbidden** | Không có quyền truy cập tài nguyên. |
| `NotFound` | **404 Not Found** | Không tìm thấy bản ghi dữ liệu yêu cầu. |
| `AlreadyExists` | **409 Conflict** | Bản ghi đã tồn tại (trùng email, username, slug...). |
| `FailedPrecondition` | **422 Unprocessable Content** | Điều kiện nghiệp vụ phía backend chưa thỏa (ví dụ xóa category đang có hàng). |
| `DeadlineExceeded` | **504 Gateway Timeout** | Backend xử lý quá hạn cho phép. |
| `Unavailable` | **503 Service Unavailable** | Service backend chưa bật hoặc bị sự cố mạng. |
| `Aborted` | **409 Conflict** | Giao dịch bị hủy do xung đột (map chung nhánh với `AlreadyExists`). |
| `ResourceExhausted` | **429 Too Many Requests** | Vượt giới hạn tài nguyên/tần suất của upstream. |
| `Unimplemented` | **501 Not Implemented** | Upstream chưa hỗ trợ rpc này (**hiện trạng 7/8 service**). |
| `StatusCode` khác | **502 Bad Gateway** | Upstream trả mã không mong đợi. |
Bảng trên khớp chính xác `Middleware/GatewayExceptionHandler.cs:61-80` (mỗi nhánh trả kèm `errorCode`):
| gRPC status | HTTP | `errorCode` |
|:---|:---:|:---|
| `InvalidArgument` | 400 | `VAL_INVALID_INPUT` |
| `Unauthenticated` | 401 | `AUTH_UNAUTHENTICATED` |
| `PermissionDenied` | 403 | `AUTH_FORBIDDEN` |
| `NotFound` | 404 | `NOT_FOUND` |
| `AlreadyExists` / `Aborted` | 409 | `CONFLICT` |
| `FailedPrecondition` | 422 | `FAILED_PRECONDITION` |
| `ResourceExhausted` | 429 | `RATE_LIMIT_EXCEEDED` |
| `Unimplemented` | 501 | `NOT_IMPLEMENTED` |
| `Unavailable` | 503 | `SERVICE_UNAVAILABLE` |
| `DeadlineExceeded` | 504 | `GATEWAY_TIMEOUT` |
| khác | 502 | `BAD_GATEWAY` |
| `OperationCanceledException` | 504 | `GATEWAY_TIMEOUT` |
| `BadHttpRequestException` | theo `StatusCode` | `BAD_REQUEST` |
| ngoại lệ khác | 500 | `INTERNAL_ERROR` |
| circuit breaker (service Unhealthy) | 503 | `DOWNSTREAM_UNHEALTHY` (`Dispatching/GatewayDispatcher.cs:103-113`) |
---
## 3.10. Tính Năng Phân Tán Trace & Giám Sát (OpenTelemetry Tracing)
- Gateway tích hợp **OpenTelemetry 1.18.0** theo chuẩn quốc tế W3C TraceContext.
- Mỗi request khi đi vào Gateway được gán một định danh theo dõi duy nhất: `traceId`.
- Gateway tự động sinh `Activity` mang tên `${service}.${operation}` gắn kèm các tags chẩn đoán:
  - `backend.service`: Tên service tiếp nhận (ví dụ: `catalog`, `commerce`).
  - `backend.mode`: Trạng thái xử lý (`Mock` hoặc `Real`).
- Header `x-trace-id` được chuyển tiếp qua gRPC Metadata xuống backend (`Dispatching/GrpcServiceCaller.cs:68`) — nhưng **chỉ khi client tương ứng đã đăng ký** (hiện chỉ `users`).
- ⚠️ **Đính chính**: exporter duy nhất được cấu hình là **OTLP**, và chỉ bật khi `OTEL_EXPORTER_OTLP_ENDPOINT` (hoặc `Otel:Endpoint`) có giá trị (`Extensions/TelemetryExtension.cs`). Repo **không** cấu hình Jaeger hay Zipkin; Jaeger/Zipkin/Tempo/Grafana đều **không** nằm trong `docker-compose.yml` của repo này.
- ⚠️ Gateway **không** có instrumentation RabbitMQ/EF Core nên không thể hiển thị chặng `Service → RabbitMQ → Database`; những chặng đó phụ thuộc việc backend tự bật OTel (xem Nguyên tắc 7 của `AGENTS.md` monorepo: package OTel hiện chỉ có ở Gateway/Log/Wallet).
---
## 3.11. Chiến Lược Phòng Thủ Xác Thực Dữ Liệu 3 Tầng (3-Tier Validation Defense-in-Depth)
Hệ thống Nexus phân định rạch ròi 3 tầng phòng vệ dữ liệu (Separation of Concerns), ngăn ngừa Gateway bị quá tải bởi các quy tắc nghiệp vụ domain riêng lẻ:
```
[ Client Request ]
       │
       ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 1: GATEWAY — KIỂM TRA MỨC HTTP & HẠ TẦNG                         │
│ - Request có đúng định dạng JSON cơ bản không (Malformed JSON).        │
│ - Kích thước gói tin có vượt quá giới hạn 64KB không.                  │
│ - Có đúng kiểu Content-Type: application/json không.                   │
│ - Rate limiting, giới hạn tần suất gọi request qua Redis.             │
│ - Kiểm tra chữ ký, thời hạn JWT và quyền RBAC cơ bản.                 │
│ - Kiểm tra header hạ tầng bắt buộc: Idempotency-Key (UUID),            │
│   biên phân trang query string (page >= 1, pageSize 1..100).           │
│ * Lưu ý: Trong Mock Mode khi backend chưa chạy, Gateway đóng vai trò   │
│   "Mock Gatekeeper" để chặn payload rác, không can thiệp sâu domain.   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (gRPC Channel)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 2: SERVICE API & APPLICATION — VALIDATE DỮ LIỆU ĐẦU VÀO (DTOs)    │
│ - Sử dụng FluentValidation hoặc Data Annotations tại tầng API/App:    │
│   + quantity > 0.                                                      │
│   + price >= 0 (hoặc decimal hợp lệ).                                 │
│   + Email đúng định dạng regex.                                       │
│   + Các trường bắt buộc không được null/empty.                        │
│   + Độ dài chuỗi tối đa/tối thiểu, regex slug/mã sản phẩm.             │
│ - Backend trả gRPC InvalidArgument: Gateway map sang HTTP 400.         │
│   Chưa có cơ chế tự động chuyển metadata backend thành `errors` map.   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ (CQRS / MediatR Handler)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│ TẦNG 3: DOMAIN VÀ DATABASE — BẢO VỆ TÍNH ĐÚNG ĐẮN BẤT BIẾN             │
│ - Domain Entities bảo vệ bất biến (Business Invariants):               │
│   + Số lượng đơn hàng phải lớn hơn 0 (order.AddItem).                  │
│   + Không cho phép trạng thái đơn hàng chuyển đổi trái quy tắc.        │
│   + Giá trị tồn kho không được âm khi trừ (inventory.Deduct).         │
│ - Database Constraints (PostgreSQL):                                   │
│   + CHECK constraints (CHECK (available >= 0), CHECK (price >= 0)).    │
│   + NOT NULL, UNIQUE (email, username, slug, idempotency_key).        │
│   + Khóa ngoại (FOREIGN KEY) bảo toàn quan hệ bảng.                    │
│ * Đảm bảo tối cao (MỤC TIÊU): Dù request đi qua Gateway, gọi gRPC nội   │
│   bộ giữa các service, hay từ RabbitMQ Consumer, dữ liệu vẫn toàn vẹn. │
│   ⚠️ Tầng 3 (Database Constraints) phụ thuộc DB của backend, KHÔNG nằm  │
│   trong repo Gateway này (gateway có 0 DbContext/0 DbSet).             │
└────────────────────────────────────────────────────────────────────────┘
```
---
## 3.12. Tính Năng Health Prober Daemon, Giám Sát Sức Khỏe & Tự Động Hồi Phục (Self-Healing)
Nhằm đảm bảo tính sẵn sàng cao (High Availability) cho toàn bộ hệ sinh thái Microservices, API Gateway tích hợp sẵn **Health Prober Daemon Engine** chạy nền, đóng vai trò là một External Prober chủ động giám sát trạng thái của 8 backend services (`users`, `catalog`, `commerce`, `log`, `auction`, `fulfillment`, `notification`, `wallet`).
### 3.12.1. Bốn Tham Số Điều Khiển Chuẩn (Configuration Standards)
Hệ thống điều khiển quy trình kiểm tra sức khỏe thông qua 4 tham số chuẩn (cấu hình qua `appsettings.json` hoặc biến môi trường `Gateway__HealthProber__...`):
- **Interval (Chu kỳ kiểm tra)**: Mặc định `60s` (1 phút). Khoảng cách thời gian giữa hai lần gõ cửa kiểm tra service.
- **Timeout (Thời gian chờ tối đa)**: Mặc định `30s`. Thời gian tối đa chờ phản hồi HTTP/gRPC từ service; nếu quá 30s coi như lần probe thất bại.
- **Retries / FailureThreshold (Ngưỡng thất bại)**: Mặc định `5 lần`. Số lần kiểm tra thất bại liên tiếp trước khi chuyển trạng thái sang `Unhealthy`.
- **StartPeriod (Thời gian ân hạn khởi động)**: Mặc định `60s` (1 phút). Khi container mới khởi động lại, trong 60s đầu tiên nếu probe thất bại sẽ không tính vào số lần Retries (để container có thời gian khởi tạo runtime và kết nối DB).
### 3.12.2. State Machine Quản Lý Vòng Đời Trạng Thái (Health State Machine)
Mỗi service được quản lý bởi một State Machine chặt chẽ:
```
    [ DISABLED ] ──(Gọi API kích hoạt)──► [ STARTING (StartPeriod 60s) ]
                                                    │
                 ┌──────────────────────────────────┴──────────────────┐
                 │ Nhận HTTP 200 OK                                    │ Hết 60s vẫn lỗi
                 ▼                                                     ▼
           [ HEALTHY ] ◄─────────────(Nhận 200 OK)───────────── [ DEGRADED (1-4 lỗi) ]
                 │                                                     │
                 │ Probe Timeout / 5xx                                 │ Lỗi tiếp chạm 5/5
                 └────────────► [ DEGRADED (1/5) ]                    ▼
                                                                [ UNHEALTHY ]
                                                                      │
                              ┌───────────────────────────────────────┴───────────────────────┐
                              │ 1. Circuit Breaker Fast-Fail: Chặn ngay request tới service ✅ │
                              │ 2. "Saga Rollback": CHỈ ghi warning log, KHÔNG bắn RabbitMQ ❌ │
                              │ 3. Docker Auto-Recovery: gọi CLI `docker restart` ⚠️           │
                              │ 4. Ghi log sự cố vào File theo cơ chế giới hạn dòng ✅         │
                              └───────────────────────────────────────┬───────────────────────┘
                                                                      ▼
                                                           [ RECOVERING (Recreated) ]
                                                                      │
                                                                      ▼
                                                           [ STARTING (Grace Period 60s) ]
```
### 3.12.3. Chiến Lược Phân Tầng Lưu Vết Log Sự Cố (Incident File Logging & Batching)
- **Với các lần check Healthy bình thường:** Tuyệt đối **không ghi ra file log** để tránh làm nghẽn Disk I/O và gây rác log (Log Pollution). Gateway lưu trữ 30 mẫu kiểm tra gần nhất trong **In-Memory Ring Buffer** phục vụ API truy vấn tức thì và đẩy metrics OpenTelemetry.
- **Khi xảy ra sự cố (Degraded 1/5 - 4/5 và Unhealthy 5/5):**
  - Gateway lưu log sự cố vào thư mục riêng: `logs/health/incident-YYYYMMDD-XXXX.log`.
  - **Cơ chế giới hạn dòng trên mỗi file (`MaxLinesPerLogFile = 1000`)**: Khi file log đạt đủ 1000 dòng, hệ thống tự động ngắt và mở file mới (`incident-YYYYMMDD-0002.log`).
- **Định hướng tương lai cho Code Agent / Developer khi triển khai `LogService` tập trung (Port 8004, `log_db`):**
  - Khi triển khai gom log tập trung về `LogService`, Gateway áp dụng cơ chế **Gom cụm tối thiểu 1000 dòng log (Batching Sink)** mới gửi bulk sang LogService qua HTTP `POST /api/v1/log/batch` hoặc RabbitMQ. Tuyệt đối không gửi từng dòng log đơn lẻ qua mạng.
### 3.12.4. Phản Ứng Tự Động Khi Service Rơi Vào Trạng Thái Unhealthy
1. **Circuit Breaker Fast-Fail Tại `GatewayDispatcher`:**
   - Khi service ở trạng thái `Unhealthy` hoặc `Recovering`, Gateway lập tức ngắt mạch trong 0ms, không đẩy request xuống mạng gRPC.
   - Trả về mã lỗi chuẩn RFC 9110: **HTTP 503 Service Unavailable** (`errorCode: "DOWNSTREAM_UNHEALTHY"`).
2. **Kích Hoạt "Saga Rollback" — ❌ CHƯA CÓ (đặc tả mục tiêu):**
   - Bản cũ mô tả Gateway publish `ServiceUnhealthyIntegrationEvent` lên RabbitMQ. **Sai với code hiện tại**: `HealthProber/SagaRollbackPublisher.cs` chỉ gọi `_logger.LogWarning("[SAGA ROLLBACK TRIGGERED] Published ServiceUnhealthyIntegrationEvent ...")` rồi `return Task.CompletedTask;`, kèm comment `// Khi có MassTransit IBus, gọi _bus.Publish(evt, cancellationToken).`
   - Repo Gateway có **0** package MassTransit/RabbitMQ (`Nexus.Gateway.csproj`), nên không thể publish. Sự kiện `ServiceUnhealthyIntegrationEvent(string ServiceName, DateTime OccurredAt, string Reason)` mới chỉ là record nội bộ.
3. **Docker Container Auto-Recovery — ⚠️ MỘT PHẦN, không chạy được trong container:**
   - `HealthProber/DockerContainerRecoveryService.cs` kiểm tra `File.Exists("/var/run/docker.sock")` rồi gọi **CLI** qua `ProcessStartInfo { FileName = "docker", Arguments = $"restart {containerName}" }` — tức `docker restart`, **không** phải `Stop → Remove → Recreate` bằng Docker Engine API như bản cũ mô tả.
   - `docker-compose.yml` **có** mount `/var/run/docker.sock:/var/run/docker.sock`, nhưng image runtime `mcr.microsoft.com/dotnet/aspnet:8.0` (`Dockerfile`) **không cài docker CLI** ⇒ trong container lời gọi `docker` sẽ thất bại (exception bị `catch` và trả `false`, chỉ ghi warning). Nếu chạy `dotnet run` trực tiếp trên máy host có docker CLI thì nhánh này mới thực thi được.
   - Tên container do `GetContainerName` map tĩnh (ví dụ `users`→`nexus-user-service`, `catalog`→`nexus-catalog-service`) — khớp quy ước `nexus-{service}-service`.
   - **Chống lặp vô hạn**: `HealthProber:AutoRecoverContainer = true`, `MaxAutoRecoverAttempts = 3` có trong `appsettings.json`. ⚠️ Cảnh báo `CRITICAL_INTERVENTION_NEEDED` chỉ là **chuỗi log mục tiêu** — không có hằng số/exception nào mang tên này trong code.
### 3.12.5. Danh Mục Endpoint Quản Trị Health Prober
Được phân quyền nghiêm ngặt theo chính sách `[Authorize(GatewayPolicies.GatewayAdmin)]` và chặn hoàn toàn bypass:
- `GET /api/v1/gateway/prober/status`: Tra cứu trạng thái toàn bộ 8 services, số lần lỗi liên tiếp, và danh sách các mẫu check gần nhất.
- `GET /api/v1/gateway/prober/status/{service}`: Tra cứu chi tiết sức khỏe của 1 service cụ thể.
- `PUT /api/v1/gateway/prober/{service}`: Bật hoặc tắt prober daemon của 1 service (hỗ trợ tùy chỉnh interval, threshold).
- `PUT /api/v1/gateway/prober/bulk`: Bật hoặc tắt hàng loạt toàn bộ 8 prober daemons cùng lúc.
- `POST /api/v1/gateway/prober/{service}/check`: **API kiểm tra sức khỏe thủ công tức thì (Manual Check on demand)** — thực thi probe ngay lập tức và trả về latency (ms), HTTP status code và kết quả.
---
# 4. BẢN ĐỒ CẤU TRÚC MÃ NGUỒN & PHÂN BỔ TRÁCH NHIỆM
```
src/Services/ApiGateway/
├── ApiGateway.sln                        # Solution file (2 project: Nexus.Gateway + Nexus.Gateway.Tests)
├── ADR.md                                # Ghi chép quyết định kiến trúc (Architecture Decision Records)
├── AGENTS.md / CLAUDE.md                 # Quy chuẩn vận hành cho AI Agent (2 file giống hệt nhau)
├── CONFIG_REFERENCE.md                   # Cẩm nang tra cứu cấu hình & biến môi trường
├── DEV_GUIDE.md                          # Hướng dẫn thêm API mới vào Gateway
├── MOCK_GUIDE.md                         # Quy trình tạo Mock API (Controller Action + Seeds + Mocks)
├── PLAN.md                               # Kế hoạch phát triển (tài liệu LỊCH SỬ, số liệu cũ đã lỗi thời)
├── README.md                             # Hướng dẫn nhanh cài đặt và khởi chạy
├── SETUP_GUIDE_SERVICES.md               # Hướng dẫn kết nối 8 microservices backend
├── dac_ta.md                             # Bản đặc tả kỹ thuật toàn diện này
├── input.md                              # Mẫu JSON envelope request/response + bảng mã lỗi
├── global.json                           # Cố định phiên bản .NET SDK (8.0.x)
├── docker-compose.yml                    # CHỈ định nghĩa service `gateway` (KHÔNG kèm Redis/Tempo/Grafana)
├── Dockerfile                            # aspnet:8.0 runtime, EXPOSE 8000, KHÔNG cài docker CLI
├── .env / .env.example                   # Biến môi trường local (.env bị gitignore, .env.example được track)
├── run-dev.sh / run-dev.ps1              # Script khởi chạy Development: `dotnet run --urls http://127.0.0.1:8000`
├── .github/workflows/ci-cd.yml           # CI build + push Docker image khi push/PR vào `main`
│
├── Nexus.Gateway/                        # PROJECT CHÍNH (ASP.NET Core 8 Web API / Gateway)
│   ├── Program.cs                        # Khởi động ứng dụng siêu tinh gọn (30 dòng)
│   ├── BackendDispatcher.cs              # Cầu nối tương thích ủy quyền sang IGatewayDispatcher
│   ├── GatewayOptions.cs                 # Cấu hình strongly-typed (CORS, RateLimit, Services, AuthMode...)
│   ├── appsettings.json                  # Cấu hình gốc — ⚠️ BỊ GITIGNORE (.gitignore:81), không commit
│   ├── appsettings.Development.json      # Cấu hình Development — ⚠️ cũng bị gitignore
│   ├── appsettings.example.json          # Bản mẫu được track (JWT key = <REPLACE_WITH_...>)
│   ├── appsettings.Development.example.json
│   ├── gateway.runtime.json              # Cấu hình runtime động (hot-reload) — ĐƯỢC TRACK trong git
│   ├── gateway.runtime.example.json
│   ├── Properties/launchSettings.json    # Profile `http` → http://localhost:8000, env Development
│   │
│   ├── Contracts/
│   │   └── shop.proto                    # Chỉ có `service UsersApi` (5 rpc) — KHÔNG phải nguồn sự thật duy nhất
│   │
│   ├── Common/                           # TẦNG DÙNG CHUNG (VALIDATION & HELPERS)
│   │   ├── ValidationHelpers.cs          # Bad(), PageOk(), GuidOk(), Roles, Consts
│   │   ├── GatewayInputException.cs      # Ngoại lệ input, errorCode mặc định VAL_INVALID_INPUT
│   │   ├── MoneyPrecision.cs             # Tiện ích giữ độ chính xác số tiền
│   │   └── Converters/PascalCaseJsonStringEnumConverter.cs  # Enum → chuỗi PascalCase
│   │
│   ├── Controllers/                      # 15 FILE controller, nhưng CHỈ 4 FILE CÓ ACTION (16 action)
│   │   ├── AuthController.cs             # ✅ 4 action: POST register, verify-otp, resend-otp, login
│   │   ├── UsersController.cs            # ✅ 1 action: GET /api/v1/users/me
│   │   ├── GatewayControlController.cs   # ✅ 6 action: gateway/modes, gateway/mode, gateway/modes/{service}, gateway/modes, mock-engine/definitions, mock-engine/reload
│   │   ├── HealthProberController.cs     # ✅ 5 action: gateway/prober/status (+/{service}), PUT {service}, PUT bulk, POST check
│   │   ├── AdminUsersController.cs       # ❌ stub 10 dòng, 0 action — route api/v1/admin/users
│   │   ├── CartController.cs             # ❌ stub — route api/v1/commerce/cart
│   │   ├── OrdersController.cs           # ❌ stub — route api/v1/commerce/orders
│   │   ├── ProductsController.cs         # ❌ stub — route api/v1/catalog/products
│   │   ├── CategoriesController.cs       # ❌ stub — route api/v1/catalog/categories
│   │   ├── InventoryController.cs        # ❌ stub — route api/v1/catalog/inventory
│   │   ├── AuctionController.cs          # ❌ stub — route api/v1/auction
│   │   ├── FulfillmentController.cs      # ❌ stub — route api/v1/fulfillment
│   │   ├── NotificationController.cs     # ❌ stub — route api/v1/notification
│   │   ├── LogController.cs              # ❌ stub — route api/v1/log
│   │   └── WalletController.cs           # ❌ stub — route api/v1/wallet
│   │
│   ├── Dtos/                             # CHỈ có 4 thư mục (không phải 11 như bản cũ)
│   │   ├── Auth/                         # AuthRequests.cs, AuthResponses.cs
│   │   ├── Users/                        # UserResponses.cs (KHÔNG có UserRequests.cs)
│   │   ├── GatewayControl/               # GatewayControlRequests.cs, GatewayControlResponses.cs
│   │   └── HealthProber/                 # HealthProberDtos.cs
│   │
│   ├── HealthProber/                     # ENGINE GIÁM SÁT SỨC KHỎE (PROBER DAEMON)
│   │   ├── HealthProberOptions.cs        # Interval 60s, Timeout 30s, FailureThreshold 5, MaxLines 1000
│   │   ├── ProberRunStatus.cs            # Disabled, Starting, Healthy, Degraded, Unhealthy, Recovering
│   │   ├── ServiceHealthState.cs         # Ring Buffer 30 mẫu gần nhất + đếm lỗi liên tiếp
│   │   ├── HealthCheckSample.cs          # Mẫu kiểm tra (latency, status code, error)
│   │   ├── IHealthProberManager.cs       # Interface quản trị vòng đời prober
│   │   ├── HealthProberManager.cs        # Background service; prober mặc định `Disabled`
│   │   ├── IHealthIncidentLogger.cs      # Interface ghi vết sự cố
│   │   ├── FileRollingHealthIncidentLogger.cs # Ghi log sự cố logs/health/, xoay file mỗi 1000 dòng
│   │   ├── IDockerContainerRecoveryService.cs # Interface khôi phục container
│   │   ├── DockerContainerRecoveryService.cs  # ⚠️ Gọi CLI `docker restart` (image không có docker CLI)
│   │   ├── ISagaRollbackPublisher.cs     # Interface phát sự kiện rollback
│   │   ├── SagaRollbackPublisher.cs      # ❌ CHỈ ghi warning log, KHÔNG publish RabbitMQ
│   │   └── HealthProberExtensions.cs     # Đăng ký DI cho Health Prober
│   │
│   ├── Dispatching/                      # PHẦN GIỮA: DISPATCHING CORE (không if/else Mock/Real ở controller)
│   │   ├── ModeEvaluator.cs              # IModeEvaluator + ModeEvaluator: đánh giá mode, header override
│   │   ├── MockDataMapper.cs             # IMockDataMapper + MockDataMapper: đọc Seed JSON mỗi request
│   │   ├── GrpcServiceCaller.cs          # IGrpcServiceCaller + GrpcServiceCaller: forward metadata & gọi gRPC
│   │   └── GatewayDispatcher.cs          # IGatewayDispatcher + GatewayDispatcher + circuit breaker 503
│   │
│   ├── Extensions/                       # MỞ RỘNG DI & PIPELINE TẬP TRUNG
│   │   ├── ConfigurationExtensions.cs    # Nạp 3 file JSON (reloadOnChange) + biến môi trường + ValidateOnStart
│   │   ├── CoreServicesExtension.cs      # Controller, Dispatching, Redis, ExceptionHandler, RequestTimeout 65s
│   │   ├── GrpcClientsExtension.cs       # ⚠️ CHỈ đăng ký 1 typed client: UsersApi ("users") — không phải 8
│   │   ├── LoggingExtensions.cs          # Serilog + PeriodicBatchingSink → GatewayBatchLogSink
│   │   ├── PipelineExtensions.cs         # Middleware pipeline chuẩn REST + custom headers
│   │   ├── SwaggerExtension.cs           # Swagger UI + Bearer Auth + MockEngineDocumentFilter
│   │   └── TelemetryExtension.cs         # AddGatewayTelemetry (OTel 1.18.0)
│   │
│   ├── Logging/
│   │   ├── GatewayBatchLogSink.cs        # ⚠️ POST /api/v1/log/batch (endpoint KHÔNG tồn tại), bỏ qua kết quả
│   │   └── LogBatchOptions.cs            # SaveBatchSnapshots, BatchDirectory, RemoteEndpoint
│   │
│   ├── Middleware/
│   │   ├── GatewayExceptionHandler.cs    # Map RpcException → HTTP RFC 9110 + errorCode
│   │   └── RedisRateLimitMiddleware.cs   # Rate limit phân tán bằng Redis (Fixed Window)
│   │
│   ├── MockEngine/                       # MOCK KHAI BÁO (declarative)
│   │   ├── IMockEngine.cs
│   │   ├── MockEngine.cs                 # Đọc *.mock.json, FileSystemWatcher debounce
│   │   ├── MockEndpointDefinition.cs     # POCO của một mock endpoint
│   │   ├── MockRouteMatcher.cs           # So khớp URL động ({id}, regex)
│   │   ├── MockRouteMatch.cs
│   │   ├── MockEngineMiddleware.cs       # Chỉ phục vụ route KHÔNG khớp ControllerActionDescriptor
│   │   └── MockEngineDocumentFilter.cs   # Đẩy mock endpoint động lên Swagger UI
│   │
│   ├── Mocks/                            # 6 file *.mock.json (+ README.md)
│   │   ├── auth-login.mock.json / auth-register.mock.json / auth-resend-otp.mock.json / auth-verify-otp.mock.json
│   │   ├── catalog-products.mock.json
│   │   └── users-me.mock.json
│   │
│   ├── Seeds/                            # CHỈ 2 thư mục có dữ liệu thật (9 file JSON)
│   │   ├── auth/                         # login.json, register.json, resend-otp.json, verify-otp.json
│   │   ├── users/                        # login.json, profile.json, register.json, resend-otp.json, verify-otp.json
│   │   ├── cart/README.md                # placeholder, KHÔNG có JSON
│   │   ├── orders/README.md              # placeholder
│   │   ├── products/README.md            # placeholder
│   │   └── README.md
│   │
│   ├── Responses/                        # ĐẦU RA: UNIFIED OUTPUT FACTORY
│   │   ├── ApiResponse.cs                # Envelope thành công (success/statusCode/data/traceId/timestamp)
│   │   ├── ApiErrorResponse.cs           # Envelope lỗi RFC 9110
│   │   ├── PagedData.cs                  # items + pagination
│   │   ├── PaginationMetadata.cs         # pageIndex/pageSize/totalCount/totalPages/hasPrevious/hasNext
│   │   ├── IApiResponseFactory.cs
│   │   ├── ApiResponseFactory.cs         # Factory chuẩn hoá response + resolve TraceId
│   │   └── HttpContextUserExtensions.cs  # Đọc ClaimsPrincipal (sub, role...)
│   │
│   ├── Security/
│   │   ├── GatewayAuthorization.cs       # Policy RBAC & attribute [DisableMockAuthorizationBypass]
│   │   ├── GatewayRequestResolution.cs   # Resolve policy/quyền yêu cầu của request
│   │   ├── GatewaySecurityHelper.cs      # Helper Mock/Real, resolve policy
│   │   ├── SecurityConfiguration.cs      # JWT Bearer, token validation, 6 policy GatewayPolicies
│   │   ├── MockSecurityAdapterMiddleware.cs # Cấp ClaimsPrincipal giả lập khi test mock
│   │   └── EdgeMiddlewareConfiguration.cs   # CORS + RateLimiter built-in + attribute BackendTag/GatewayRateLimit
│   │
│   ├── Services/
│   │   ├── IGatewayRuntimeManager.cs     # GetAuthMode/SetAuthModeAsync/GetServiceModes/Set(Bulk)ServiceMode...
│   │   └── GatewayRuntimeManager.cs      # Quản lý Mock/Real theo service, ghi đè gateway.runtime.json
│   │
│   └── logs/                             # ⚠️ log runtime (9 file batch-*.json đang bị COMMIT vào git)
│
└── Nexus.Gateway.Tests/                  # 19 file test / 62 test attribute ([Fact] 49 + [Theory] 13)
    ├── Factory.cs                        # Helper khởi tạo WebApplicationFactory (không chứa test)
    ├── GatewayTests.cs / SevenServicesGatewayTests.cs / GatewaySurfaceTests.cs
    ├── MockEngineTests.cs / MockPaginationValidationTests.cs / BulkModesAtomicityTests.cs
    ├── HealthProberControllerTests.cs / HealthProberUnitTests.cs
    ├── EndpointRateLimitingTests.cs / RedisRateLimitMiddlewareRegressionTests.cs
    ├── ApiResponseFactoryTests.cs / ModelBindingResponseTests.cs / PaginationRequestBindingTests.cs
    ├── EnumConversionTests.cs / MoneyPrecisionHttpTests.cs / TraceConsistencyTests.cs
    └── GatewayInputExceptionTests.cs / GatewayRequestResolutionTests.cs
```
# 5. BẢNG TỔNG HỢP 15 CONTROLLERS & DANH MỤC CÁC NHÓM ROUTE
Toàn bộ các endpoint trong Gateway đều bắt đầu bằng tiền tố duy nhất:
$$\mathbf{/api/v1/...}$$
> ⚠️ **Đính chính quan trọng**: bảng dưới đây liệt kê **15 file controller có thật**, nhưng **chỉ 4 file có action (tổng 16 action)**. 11 file còn lại là stub đúng 10 dòng, chứa một dòng `// TODO (Team)` và **0 action** — mọi route ghi trong cột "Chức Năng Nghiệp Vụ" của các file đó là **đặc tả mục tiêu, chưa triển khai**. Cột `Trạng thái`/`Bằng chứng` phản ánh đúng code hiện tại.
| STT | Controller | File Code | Route Prefix (thật) | Quyền Hạn (RBAC) | Chức Năng Nghiệp Vụ | Service Đích | Trạng thái | Bằng chứng |
|:---:|:---|:---|:---|:---|:---|:---:|:---|:---|
| 1 | `AuthController` | `Controllers/AuthController.cs` | `/api/v1/auth` | `[AllowAnonymous]` | Đăng ký (`POST /register`), xác thực OTP (`POST /verify-otp`), gửi lại OTP (`POST /resend-otp`), đăng nhập (`POST /login`). | `users` | ✅ CÓ — 4 action | `AuthController.cs:14-17,29,61,84,106`; `[EnableRateLimiting("auth-strict")]` |
| 2 | `UsersController` | `Controllers/UsersController.cs` | `/api/v1` + action `users/me` | `[Authorize("authenticated")]` | **Chỉ** xem hồ sơ cá nhân (`GET /api/v1/users/me`). Cập nhật hồ sơ / `seller-upgrade` | `users` | ⚠️ MỘT PHẦN — 1/3 action; 2 action kia ❌ | `UsersController.cs:12-15,27-28` |
| 3 | `AdminUsersController` | `Controllers/AdminUsersController.cs` | `/api/v1/admin/users` | *(chưa khai báo)* | Đặc tả mục tiêu: duyệt seller, gán role, xem roles/permissions | `users` | ❌ CHƯA CÓ — stub 10 dòng, 0 action | `AdminUsersController.cs` (10 dòng) |
| 4 | `CartController` | `Controllers/CartController.cs` | `/api/v1/commerce/cart` | *(chưa khai báo)* | Đặc tả mục tiêu: xem giỏ, thêm/sửa/xóa item, xóa trắng giỏ | `commerce` | ❌ CHƯA CÓ — stub | `CartController.cs` (10 dòng) |
| 5 | `OrdersController` | `Controllers/OrdersController.cs` | `/api/v1/commerce/orders` | *(chưa khai báo)* | Đặc tả mục tiêu: checkout, danh sách/chi tiết đơn, seller/admin orders | `commerce` | ❌ CHƯA CÓ — stub | `OrdersController.cs` (10 dòng) |
| 6 | `ProductsController` | `Controllers/ProductsController.cs` | `/api/v1/catalog/products` | *(chưa khai báo)* | Đặc tả mục tiêu: danh sách/chi tiết sản phẩm; seller CRUD | `catalog` | ❌ CHƯA CÓ — stub | `ProductsController.cs` (10 dòng) |
| 7 | `CategoriesController` | `Controllers/CategoriesController.cs` | `/api/v1/catalog/categories` | *(chưa khai báo)* | Đặc tả mục tiêu: xem danh mục; admin CRUD danh mục | `catalog` | ❌ CHƯA CÓ — stub | `CategoriesController.cs` (10 dòng) |
| 8 | `InventoryController` | `Controllers/InventoryController.cs` | `/api/v1/catalog/inventory` | *(chưa khai báo)* | Đặc tả mục tiêu: quản lý kho ảo seller, điều chỉnh tồn kho | `catalog` | ❌ CHƯA CÓ — stub | `InventoryController.cs` (10 dòng); route cũ `/api/v1/seller/inventory` trong tài liệu là **sai** |
| 9 | `AuctionController` | `Controllers/AuctionController.cs` | `/api/v1/auction` | *(chưa khai báo)* | Đặc tả mục tiêu: danh sách/chi tiết phiên, tạo phiên, đặt bid | `auction` | ❌ CHƯA CÓ — stub | `AuctionController.cs` (10 dòng); route cũ `/api/v1/auctions` là **sai** |
| 10 | `FulfillmentController` | `Controllers/FulfillmentController.cs`| `/api/v1/fulfillment` | *(chưa khai báo)* | Đặc tả mục tiêu: tồn kho vật lý, phí ship, tra mã vận đơn | `fulfillment` | ❌ CHƯA CÓ — stub | `FulfillmentController.cs` (10 dòng) |
| 11 | `NotificationController`| `Controllers/NotificationController.cs`| `/api/v1/notification` | *(chưa khai báo)* | Đặc tả mục tiêu: danh sách thông báo, đánh dấu đã đọc, preferences | `notification` | ❌ CHƯA CÓ — stub | `NotificationController.cs` (10 dòng); route cũ `/api/v1/notifications` là **sai** (thiếu `s` trong code) |
| 12 | `LogController` | `Controllers/LogController.cs` | `/api/v1/log` | *(chưa khai báo)* | Đặc tả mục tiêu: tra cứu nhật ký, chi tiết log, event types | `log` | ❌ CHƯA CÓ — stub | `LogController.cs` (10 dòng) |
| 13 | `WalletController` | `Controllers/WalletController.cs` | `/api/v1/wallet` | *(chưa khai báo)* | Đặc tả mục tiêu: số dư, nạp/rút, chuyển tiền, lịch sử giao dịch | `wallet` | ❌ CHƯA CÓ — stub | `WalletController.cs` (10 dòng) |
| 14 | `GatewayControlController`| `Controllers/GatewayControlController.cs`| `/api/v1` + action `gateway/*`, `mock-engine/*` | `GatewayAdmin` + `[DisableMockAuthorizationBypass]` | `GET gateway/modes`, `PUT gateway/mode`, `PUT gateway/modes/{service}`, `PUT gateway/modes`, `GET mock-engine/definitions`, `POST mock-engine/reload` | *Nội bộ Gateway* | ✅ CÓ — 6 action | `GatewayControlController.cs:13-17,34,42,58,74,101,120` |
| 15 | `HealthProberController` | `Controllers/HealthProberController.cs` | `/api/v1` + action `gateway/prober/*` | `GatewayAdmin` + `[DisableMockAuthorizationBypass]` | `GET gateway/prober/status`, `GET .../status/{service}`, `PUT .../{service}`, `PUT .../bulk`, `POST .../{service}/check` | *Nội bộ Gateway* | ✅ CÓ — 5 action | `HealthProberController.cs:15-19,33,51,79,110,124` |
**Tổng kết kiểm chứng**: 15 file · 4 file có action · **16 action** (5 `[HttpGet]`, 6 `[HttpPost]`, 5 `[HttpPut]`) · 11 stub × 10 dòng · 0 action ở 11 file stub.
---
# 6. QUY TRÌNH 3 BƯỚC THÊM API MỚI DÀNH CHO DEVELOPER
Nhờ kiến trúc 3 khối độc lập và bộ điều phối `GatewayDispatcher`, quy trình bổ sung một API mới diễn ra nhanh chóng và không làm xáo trộn mã nguồn.
> ⚠️ **Ví dụ dưới đây là ĐẶC TẢ MỤC TIÊU, CHƯA TRIỂN KHAI**: `service CatalogApi`, `rpc ListBrands`, message `BrandPage`/`Brand`/`PageRequest` **không tồn tại** trong `Nexus.Gateway/Contracts/shop.proto` hiện tại (file chỉ có `service UsersApi` với 5 rpc: `Register`, `Login`, `VerifyOtp`, `ResendOtp`, `GetProfile`). Không có `CatalogApiClient` nào được sinh ra. Tên file controller thật cũng **không** phải `CatalogController.cs` (xem Bước 3).
### Bước 1: Khai báo Contract trong `Contracts/shop.proto`
Thêm service/rpc mới vào file Protobuf hiện có (`Nexus.Gateway/Contracts/shop.proto`, `csharp_namespace = "ShoppingCart.Contracts.V1"`, `package shoppingcart.v1`). Ví dụ **mục tiêu** nếu muốn thêm nhóm Catalog:
```protobuf
// Đặc tả mục tiêu — service này CHƯA tồn tại trong shop.proto
service CatalogApi {
  rpc ListBrands(PageRequest) returns (BrandPage);
}
message BrandPage {
  repeated Brand items = 1;
  int32 total = 2;
}
message Brand {
  string id = 1;
  string name = 2;
  string logo_url = 3;
}
```
*(Sau khi thêm, `Grpc.Tools 2.66.0` sinh mã C# cho `BrandPage`, `Brand`, `CatalogApiClient` lúc build. `Nexus.Gateway.csproj:27` khai báo `<Protobuf Include="Contracts/shop.proto" GrpcServices="Both" />`.)*
---
### Bước 2: Tạo File Seed Mẫu Tại `Seeds/catalog/brands.json`
Tạo file JSON mẫu tại `Nexus.Gateway/Seeds/catalog/brands.json`:
```json
{
  "items": [
    { "id": "brand-01", "name": "Keychron", "logoUrl": "https://example.com/keychron.png" },
    { "id": "brand-02", "name": "Akko", "logoUrl": "https://example.com/akko.png" }
  ],
  "total": 2
}
```
*(Đường dẫn này **chưa tồn tại**: `Seeds/` hiện chỉ có `auth/` (4 file), `users/` (5 file) và 3 thư mục placeholder `cart/`, `orders/`, `products/` chỉ chứa README. `Seeds/**/*.json` được copy ra output qua `Nexus.Gateway.csproj:29`.)*
⚠️ Cơ chế đọc seed: `Dispatching/MockDataMapper.cs` đọc file bằng `File.ReadAllTextAsync` **mỗi request** (không cache), rồi parse bằng `MessageParser` của chính message protobuf tương ứng — nếu message không tồn tại (như `BrandPage` hiện nay) thì không có parser để validate.
---
### Bước 3: Thêm Action Trong Controller Tương Ứng
File thật cần sửa là `Nexus.Gateway/Controllers/ProductsController.cs` (hiện là **stub 10 dòng**, `[Route("api/v1/catalog/products")]`, 0 action) — **không có** `CatalogController.cs` trong repo.
Action chỉ khai báo route **tương đối**, policy, kiểu request HTTP và lời gọi gRPC typed; không tự dựng JSON response và **không** tự rẽ nhánh Mock/Real:
```csharp
// Đặc tả mục tiêu — chỉ biên dịch được sau khi Bước 1 hoàn tất
[HttpGet("catalog/brands")]
[AllowAnonymous]
[ProducesResponseType(typeof(ApiResponse<PagedData<Brand>>), StatusCodes.Status200OK)]
public Task<IResult> ListBrands([FromQuery] PaginationRequest pagination)
{
    if (!pagination.IsValid)
        throw new GatewayInputException(ValidationHelpers.PageMessage, field: "Pagination");
    return _dispatcher.DispatchPagedAsync(
        HttpContext, "catalog", "brands", pagination.Page, pagination.PageSize,
        o => _client.ListBrandsAsync(new PageRequest
        {
            Page = pagination.Page,
            PageSize = pagination.PageSize
        }, o),
        BrandPage.Parser);
}
```
Ghi chú kỹ thuật đã đối chiếu code:
- `IGatewayDispatcher.DispatchPagedAsync<T>(HttpContext, string service, string operation, int pageIndex, int pageSize, Func<CallOptions, AsyncUnaryCall<T>> realCall, MessageParser<T> parser, int successStatusCode = 200)` — `Nexus.Gateway/Dispatching/GatewayDispatcher.cs:22-33` (đúng thứ tự tham số trong ví dụ).
- `PaginationRequest` là HTTP query model (`page`, `pageSize`); **không thay** các request protobuf.
- `GatewayInputException` mặc định `errorCode = "VAL_INVALID_INPUT"` (`Common/GatewayInputException.cs:9`).
- Nếu route mới nằm ngoài prefix `api/v1/catalog/products` của file stub, phải sửa `[Route(...)]` hoặc dùng route tuyệt đối — nếu không sẽ **không** khớp endpoint nào.
👉 **Hoàn tất (khi đã làm đủ 3 bước)**
- Ở mode `Mock`: Gateway đọc file `Seeds/catalog/brands.json` và trả `ApiResponse<PagedData<Brand>>`.
- Ở mode `Real`: Gateway forward gRPC sang Catalog service, kèm `x-trace-id`, `x-user-id`, `x-user-roles`, `Idempotency-Key` (`Dispatching/GrpcServiceCaller.cs:62-89`).
- Endpoint mới hiển thị trên Swagger UI.
---
# 7. BẢO ĐẢM CHẤT LƯỢNG & KIỂM THỬ TỰ ĐỘNG
Bộ kiểm thử nằm trong `Nexus.Gateway.Tests/` — **19 file, 62 test attribute** (`[Fact]` 49 + `[Theory]` 13, kèm 49 dòng `[InlineData]`), tức khoảng **85 test case** khi chạy. **Không** có con số "120/120" hay "98/98" trong repo; muốn số pass thật hãy chạy lệnh bên dưới và đọc output.
| File test | `[Fact]` | `[Theory]` | Nội dung kiểm chứng |
|:---|:---:|:---:|:---|
| `ApiResponseFactoryTests.cs` | 3 | 0 | Envelope thành công + Problem Details RFC 9110 |
| `BulkModesAtomicityTests.cs` | 0 | 1 | Bulk update sai **không** làm thay đổi auth mode/service mode |
| `EndpointRateLimitingTests.cs` | 5 | 0 | Rate limit theo endpoint |
| `EnumConversionTests.cs` | 4 | 1 | Enum 2 chiều: serialize PascalCase, deserialize case-insensitive, chặn giá trị số |
| `GatewayInputExceptionTests.cs` | 0 | 1 | Mã lỗi mặc định `VAL_INVALID_INPUT` |
| `GatewayRequestResolutionTests.cs` | 3 | 1 | Resolve tên service chuẩn hoá (user→users, catalog→catalog...) |
| `GatewaySurfaceTests.cs` | 3 | 0 | Bề mặt tổng quát của Gateway — **không** phải "toàn bộ API của 8 services" |
| `GatewayTests.cs` | 5 | 1 | Luồng mock/jwt/OTP/real-mock coexist, production từ chối mock & plaintext |
| `HealthProberControllerTests.cs` | 4 | 0 | Phân quyền admin, bật/tắt daemon, manual check, circuit breaker 503 |
| `HealthProberUnitTests.cs` | 2 | 0 | Logic state machine / trạng thái mặc định của prober |
| `MockEngineTests.cs` | 10 | 0 | Khớp route động, ưu tiên route tĩnh, nạp mock JSON |
| `MockPaginationValidationTests.cs` | 0 | 1 | Validate phân trang trong mock engine |
| `ModelBindingResponseTests.cs` | 1 | 0 | Model binding trả lỗi |
| `MoneyPrecisionHttpTests.cs` | 0 | 2 | Giữ độ chính xác số tiền qua HTTP |
| `PaginationRequestBindingTests.cs` | 1 | 2 | Bind `page`/`pageSize` |
| `RedisRateLimitMiddlewareRegressionTests.cs` | 0 | 3 | Regression rate limit Redis |
| `SevenServicesGatewayTests.cs` | 5 | 0 | Một số luồng Mock/Real + auth — **không** thay thế integration test của 8 backend |
| `TraceConsistencyTests.cs` | 3 | 0 | Trace ID nhất quán |
### Lệnh chạy kiểm thử:
```bash
dotnet test ApiGateway.sln -c Release
```
👉 Lấy số liệu pass/fail **từ output thật** của lệnh trên. Bản cũ ghi "120/120 bài kiểm thử đều vượt qua (100% Passed)" — con số này **không tồn tại** trong repo và không kiểm chứng được. Đặc biệt kiểm tra auth `Enforce`/`Bypass`, mode Mock/Real, HTTP 200/201/4xx, lỗi typed/malformed JSON, route phân trang và `traceId`.